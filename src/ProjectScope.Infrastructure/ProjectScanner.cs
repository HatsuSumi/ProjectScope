using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ProjectScope.Core;

namespace ProjectScope.Infrastructure;

public sealed class ProjectScanner : IProjectScanner
{
    private readonly FileCatalog _catalog;
    private readonly CodeAnalyzer _analyzer;

    public ProjectScanner(FileCatalog catalog, CodeAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(analyzer);
        _catalog = catalog;
        _analyzer = analyzer;
    }

    public Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        var root = Path.GetFullPath(request.RootPath);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Scan directory does not exist: {root}");
        return Task.Run(() => Scan(root, request.Options, progress, cancellationToken), cancellationToken);
    }

    private async Task<ScanResult> Scan(string root, ScanOptions options, IProgress<ScanProgress> progress, CancellationToken token)
    {
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var ignoredDirectories = options.IgnoredDirectoryNames.ToHashSet(pathComparer);
        var rules = options.IgnoredPaths.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        var regexOptions = RegexOptions.CultureInvariant;
        if (OperatingSystem.IsWindows()) regexOptions |= RegexOptions.IgnoreCase;
        var ignoredPatterns = rules.Where(value => value.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)).Select(value =>
        {
            var pattern = value[6..].Trim();
            if (pattern.Length == 0) throw new ArgumentException("忽略路径正则表达式不能为空：" + value);
            try { return new Regex(pattern, regexOptions, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException exception) { throw new ArgumentException($"忽略路径正则表达式无效：{value}。{exception.Message}", exception); }
        }).ToArray();
        var ignoredPaths = rules.Where(value => !value.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)).Select(value =>
        {
            var relative = value.Trim().Replace('\\', '/').Trim('/');
            var fullPath = Path.GetFullPath(Path.Combine(root, relative));
            var normalized = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized == ".." || normalized.StartsWith("../", StringComparison.Ordinal))
                throw new ArgumentException($"忽略路径必须位于项目根目录内：{value}");
            return normalized;
        }).ToHashSet(pathComparer);
        var files = new List<string>();
        var counts = new Dictionary<string, (string Label, int Count)>();
        var code = new Dictionary<string, CodeStatistics>();
        var assets = new Dictionary<string, (string Label, AssetStatistics Statistics)>();
        var fileDetails = new Dictionary<(string Type, string Extension), int>();
        var assetDetails = new Dictionary<(string Type, string Extension), int>();
        var pending = new Stack<string>();
        pending.Push(root);
        var timer = Stopwatch.StartNew();
        long processed = 0;
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            IEnumerable<string> entries;
            try
            {
                var collected = new List<string>();
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    token.ThrowIfCancellationRequested();
                    collected.Add(entry);
                }
                token.ThrowIfCancellationRequested();
                collected.Sort(StringComparer.Ordinal);
                token.ThrowIfCancellationRequested();
                entries = collected;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw new IOException($"Cannot enumerate directory '{directory}'.", exception); }
            foreach (var path in entries)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var attributes = File.GetAttributes(path);
                    var name = Path.GetFileName(path);
                    var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
                    if (ignoredPaths.Contains(relativePath) || ignoredPatterns.Any(pattern => pattern.IsMatch("/" + relativePath))) continue;
                    if (attributes.HasFlag(FileAttributes.Directory) && ignoredDirectories.Contains(name)) continue;
                    if (!options.IncludeHidden && (name.StartsWith('.') || attributes.HasFlag(FileAttributes.Hidden))) continue;
                    if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException($"Symbolic links and junctions are not supported: {path}");
                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        pending.Push(path);
                        continue;
                    }
                    files.Add(relativePath);
                    processed++;
                    var extension = Path.GetExtension(path);
                    var binary = _catalog.IsBinaryExtension(path) || ContainsNullByte(path);
                    if (!binary)
                    {
                        var definition = _catalog.DetectTextType(path);
                        Increment(counts, definition.Name, definition.Label);
                        Increment(fileDetails, (definition.Name, extension));
                        if (definition.IsCode)
                        {
                            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
                            var (lines, characters) = options.ExcludeBlankLinesAndComments
                                ? _analyzer.Analyze(text, definition.CommentSyntax, token)
                                : _analyzer.AnalyzeRaw(text, token);
                            code.TryGetValue(definition.Name, out var previous);
                            if (previous is null) previous = new(0, 0, 0);
                            code[definition.Name] = new(previous.Files + 1, previous.Lines + lines, previous.Characters + characters);
                        }
                    }
                    if (options.IncludeAssets) AccumulateAsset(path, extension, binary, assets, assetDetails);
                    if (timer.ElapsedMilliseconds >= 100)
                    {
                        progress.Report(new(path, processed));
                        timer.Restart();
                    }
                }
                catch (RegexMatchTimeoutException exception)
                {
                    throw new ArgumentException($"忽略路径正则匹配超时：regex:{exception.Pattern}。请简化表达式。", exception);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
                {
                    throw new IOException($"Scanning failed at '{path}': {exception.Message}", exception);
                }
            }
        }
        token.ThrowIfCancellationRequested();
        files.Sort(StringComparer.Ordinal);
        var codeRowsRaw = code.OrderByDescending(item => item.Value.Lines).ThenBy(item => item.Key, StringComparer.Ordinal).ToArray();
        var lineShares = PercentageAllocator.Allocate(codeRowsRaw.Select(item => item.Value.Lines).ToArray());
        var characterShares = PercentageAllocator.Allocate(codeRowsRaw.Select(item => item.Value.Characters).ToArray());
        var codeRows = codeRowsRaw.Select((item, index) => new CodeTypeCount(item.Key, item.Key, item.Value.Files, item.Value.Lines, item.Value.Characters, lineShares[index], characterShares[index])).ToArray();
        var assetRows = assets.OrderByDescending(item => item.Value.Statistics.Bytes).ThenBy(item => item.Key, StringComparer.Ordinal).Select(item => new AssetTypeCount(item.Key, item.Value.Label, item.Value.Statistics.Files, item.Value.Statistics.Bytes)).ToArray();
        return new(root, files.Count, counts.OrderByDescending(item => item.Value.Count).ThenBy(item => item.Key, StringComparer.Ordinal).Select(item => new TypeCount(item.Key, item.Value.Label, item.Value.Count)).ToArray(), codeRows, assetRows, codeRowsRaw.Sum(item => item.Value.Files), codeRowsRaw.Sum(item => item.Value.Lines), codeRowsRaw.Sum(item => item.Value.Characters), assetRows.Sum(item => item.Files), assetRows.Sum(item => item.Bytes), files.ToArray(), DirectoryTreeFormatter.Format(new DirectoryInfo(root).Name, files), ToDetails(fileDetails, "file"), ToDetails(assetDetails, "asset"));
    }

    private void AccumulateAsset(string path, string extension, bool binary, Dictionary<string, (string Label, AssetStatistics Statistics)> assets, Dictionary<(string Type, string Extension), int> details)
    {
        var recognized = _catalog.TryDetectAsset(path, out var asset);
        if (!recognized && !binary) return;
        if (!recognized) asset = new("other_asset", "其他资源文件", System.Collections.Frozen.FrozenSet<string>.Empty);
        assets.TryGetValue(asset.Name, out var previous);
        var statistics = previous.Statistics;
        if (statistics is null) statistics = new(0, 0);
        assets[asset.Name] = (asset.Label, new(statistics.Files + 1, statistics.Bytes + new FileInfo(path).Length));
        Increment(details, (asset.Name, extension));
    }

    private static bool ContainsNullByte(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> buffer = stackalloc byte[4096];
        var length = stream.Read(buffer);
        return buffer[..length].Contains((byte)0);
    }

    private static void Increment(Dictionary<string, (string Label, int Count)> counts, string key, string label)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = (label, current.Count + 1);
    }

    private static void Increment(Dictionary<(string Type, string Extension), int> counts, (string Type, string Extension) key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static IReadOnlyList<DetailCount> ToDetails(Dictionary<(string Type, string Extension), int> counts, string category) => counts.OrderBy(item => item.Key.Type, StringComparer.Ordinal).ThenBy(item => item.Key.Extension, StringComparer.Ordinal).Select(item => new DetailCount(category, item.Key.Type, item.Key.Extension.Length == 0 ? "(无后缀)" : item.Key.Extension, item.Value)).ToArray();
}
