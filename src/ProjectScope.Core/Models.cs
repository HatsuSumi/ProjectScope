namespace ProjectScope.Core;

public sealed record CodeStatistics(int Files, long Lines, long Characters);
public sealed record AssetStatistics(int Files, long Bytes);
public sealed record TypeCount(string Key, string Label, int Count);
public sealed record CodeTypeCount(string Key, string Label, int Files, long Lines, long Characters, double LinePercentage, double CharacterPercentage);
public sealed record AssetTypeCount(string Key, string Label, int Files, long Bytes);
public sealed record DetailCount(string Category, string Type, string Extension, int Count);
public sealed record ScanOptions(bool IncludeAssets, bool IncludeHidden)
{
    public IReadOnlyList<string> IgnoredDirectoryNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IgnoredPaths { get; init; } = Array.Empty<string>();
    public bool ExcludeBlankLinesAndComments { get; init; } = true;
}
public sealed record ScanProgress(string CurrentPath, long ProcessedFiles);
public sealed record ScanResult(string RootPath, int TotalFiles, IReadOnlyList<TypeCount> FileCounts, IReadOnlyList<CodeTypeCount> CodeTypes, IReadOnlyList<AssetTypeCount> AssetTypes, long TotalCodeFiles, long TotalCodeLines, long TotalCodeCharacters, int TotalAssetFiles, long TotalAssetBytes, IReadOnlyList<string> Files, string DirectoryTree, IReadOnlyList<DetailCount> Details, IReadOnlyList<DetailCount> AssetDetails);

public sealed class ScanRequest
{
    public ScanRequest(string rootPath, ScanOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);
        RootPath = rootPath;
        Options = options;
    }

    public string RootPath { get; }
    public ScanOptions Options { get; }
}
