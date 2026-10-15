using System.Text;
using ProjectScope.Core;
using ProjectScope.Infrastructure;

namespace ProjectScope.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public void FileQueryMatchesNamesPathsCaseInsensitivelyAndReturnsAllMatches()
    {
        var files = new[]
        {
            "src/ProjectScope.App/MainViewModel.cs",
            "src/ProjectScope.Tests/MainViewModelTests.cs",
            "README.md"
        };

        var nameMatches = ProjectScope.App.MainViewModel.FindFileMatches(files, "MAINVIEWMODEL");
        var pathMatches = ProjectScope.App.MainViewModel.FindFileMatches(files, @"projectscope.app\mainviewmodel.cs");

        Assert.Equal(new[]
        {
            "src/ProjectScope.App/MainViewModel.cs",
            "src/ProjectScope.Tests/MainViewModelTests.cs"
        }, nameMatches);
        Assert.Equal(new[] { "src/ProjectScope.App/MainViewModel.cs" }, pathMatches);
    }
    [Fact]
    public void FileCatalogRecognizesDartAsCode()
    {
        var definition = new FileCatalog().DetectTextType("lib/main.dart");
        Assert.Equal("Dart", definition.Name);
        Assert.True(definition.IsCode);
        Assert.Equal(CommentSyntax.CLike, definition.CommentSyntax);
    }
    [Fact]
    public void FileCatalogMatchesScriptLanguageExtensions()
    {
        var catalog = new FileCatalog();
        Assert.Equal("Objective-C", catalog.DetectTextType("main.m").Name);
        Assert.Equal("Kotlin", catalog.DetectTextType("Main.kt").Name);
        Assert.Equal("Shader", catalog.DetectTextType("effect.cginc").Name);
        Assert.Equal("D", catalog.DetectTextType("module.d").Name);
        Assert.Equal("Ruby", catalog.DetectTextType("Gemfile.rake").Name);
        Assert.Equal("XML", catalog.DetectTextType("MainWindow.axaml").Name);
        Assert.True(catalog.DetectTextType("MainWindow.axaml").IsCode);
        Assert.Equal(CommentSyntax.Html, catalog.DetectTextType("MainWindow.axaml").CommentSyntax);
    }
    [Fact]
    public void CLikeCommentRemovalPreservesCommentMarkersInsideStrings()
    {
        var analyzer = new CodeAnalyzer();
        var source = "const url = \"https://example.test/a//b\"; // remove this\nconst value = 3; /* and this */";
        var result = analyzer.Analyze(source, CommentSyntax.CLike);
        Assert.Equal(2, result.Lines);
        Assert.Equal("const url = \"https://example.test/a//b\"; ".Length + "const value = 3; ".Length, result.Characters);
    }


    [Fact]
    public void PythonDocstringsAreExcludedAndUnicodeCountsAsCodePoints()
    {
        var analyzer = new CodeAnalyzer();
        var source = "def greet():\n    \"\"\"说明😀\"\"\"\n    return '你好😀' # 注释";
        var result = analyzer.Analyze(source, CommentSyntax.Python);
        Assert.Equal(2, result.Lines);
        Assert.Equal(29, result.Characters);
    }

    [Fact]
    public void PercentagesAreRoundedToOneDecimalAndSumToOneHundred()
    {
        var percentages = PercentageAllocator.Allocate(new long[] { 1, 1, 1 });
        Assert.Equal(100.0, Math.Round(percentages.Sum(), 1));
        Assert.Equal(new[] { 33.4, 33.3, 33.3 }, percentages);
    }

    [Fact]
    public void DirectoryTreeIsStableAndIncludesNestedDirectories()
    {
        var result = DirectoryTreeFormatter.Format("Demo", new[] { "z.cs", "src/main.cs", "assets/icon.png" });
        Assert.Contains("├── assets/", result);
        Assert.Contains("├── src/", result);
        Assert.True(result.IndexOf("assets/", StringComparison.Ordinal) < result.IndexOf("z.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScannerIncludesDependencyAndGeneratedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "main.cs"), "class Demo { } // 注释\n");
        await File.WriteAllTextAsync(Path.Combine(root, "model3.json"), "{\"ok\": true}");
        await File.WriteAllBytesAsync(Path.Combine(root, "icon.png"), new byte[] { 0x89, 0x50, 0x4e, 0x47 });
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "dependency.js"), "const dependency = true;");
        await File.WriteAllTextAsync(Path.Combine(root, "model.g.dart"), "class Model {}");
        await File.WriteAllTextAsync(Path.Combine(root, "scan.log"), "scan complete");
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var options = new ScanOptions(true, false);
            var result = await scanner.ScanAsync(new(root, options), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(6, result.TotalFiles);
            Assert.Equal(1, result.TotalAssetFiles);
            Assert.Equal(4, result.TotalCodeFiles);
            Assert.Equal(6, result.Files.Count);
            Assert.Contains("main.cs", result.DirectoryTree);
            Assert.Contains("node_modules/dependency.js", result.Files);
            Assert.Contains("model.g.dart", result.Files);
            Assert.Contains("scan.log", result.Files);
            Assert.Contains(result.AssetTypes, item => item.Key == "image" && item.Files == 1);
            Assert.Contains(result.Details, item => item.Category == "file" && item.Extension == ".cs");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerAppliesSelectedDirectoriesAndRootRelativePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        var paths = new[]
        {
            "node_modules/dependency.js", "src/node_modules/dependency.js",
            "A/B/example.xxx", "A/C/example.xxx", "A/D/example.xxx",
            "A/cache/nested/data.json", "A/cache-extra/data.json", "model.g.dart"
        };
        try
        {
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, "{}");
            }
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var options = new ScanOptions(true, true)
            {
                IgnoredDirectoryNames = new[] { "node_modules" },
                IgnoredPaths = new[] { "/A/B/example.xxx", @"\A\C\example.xxx", "/A/cache/" }
            };
            var result = await scanner.ScanAsync(new(root, options), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(new[] { "A/D/example.xxx", "A/cache-extra/data.json", "model.g.dart" }, result.Files);
            Assert.DoesNotContain("node_modules", result.DirectoryTree);
            Assert.Equal(3, result.TotalFiles);

            var unfiltered = await scanner.ScanAsync(new(root, new ScanOptions(true, true)), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(paths.Length, unfiltered.TotalFiles);

            var regexOptions = options with
            {
                IgnoredPaths = new[] { @"regex:^/A/(B|C)/example\.xxx$", "regex:^/A/cache$", @"regex:\.g\.dart$" }
            };
            var regexResult = await scanner.ScanAsync(new(root, regexOptions), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(new[] { "A/D/example.xxx", "A/cache-extra/data.json" }, regexResult.Files);

            foreach (var invalidRule in new[] { "regex:[", "regex:" })
            {
                var invalidOptions = options with { IgnoredPaths = new[] { invalidRule } };
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync(new(root, invalidOptions), new Progress<ScanProgress>(), CancellationToken.None));
                Assert.Contains("正则表达式", exception.Message);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerCanIncludeBlankLinesAndCommentsInCodeStatistics()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "main.cs"), "class Demo {} // code\n// comment\n\n");
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var filtered = await scanner.ScanAsync(new(root, new ScanOptions(false, false)), new Progress<ScanProgress>(), CancellationToken.None);
            var raw = await scanner.ScanAsync(new(root, new ScanOptions(false, false) { ExcludeBlankLinesAndComments = false }), new Progress<ScanProgress>(), CancellationToken.None);

            Assert.Equal(1, filtered.TotalCodeLines);
            Assert.Equal(3, raw.TotalCodeLines);
            Assert.True(raw.TotalCodeCharacters > filtered.TotalCodeCharacters);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerAlwaysIncludesFileListAndDetails()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "main.js"), "const value = 1;");
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var result = await scanner.ScanAsync(new(root, new ScanOptions(false, false)), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(new[] { "main.js" }, result.Files);
            Assert.Contains(result.Details, item => item.Type == "JavaScript" && item.Extension == ".js" && item.Count == 1);
            Assert.Contains("main.js", result.DirectoryTree);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerSkipsLockedFilesAndContinuesScanning()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var lockedPath = Path.Combine(root, "locked.cs");
        var readablePath = Path.Combine(root, "readable.cs");
        await File.WriteAllTextAsync(lockedPath, "class Locked {}");
        await File.WriteAllTextAsync(readablePath, "class Readable {}");
        try
        {
            using var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var result = await scanner.ScanAsync(new(root, new ScanOptions(false, false)), new Progress<ScanProgress>(), CancellationToken.None);

            Assert.Contains("readable.cs", result.Files);
            if (OperatingSystem.IsWindows()) Assert.DoesNotContain("locked.cs", result.Files);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerReadsLegacyWindowsTextWithoutFailingTheScan()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var legacyPath = Path.Combine(root, "legacy.ps1");
        var readablePath = Path.Combine(root, "readable.cs");
        await File.WriteAllBytesAsync(legacyPath, Encoding.GetEncoding(1252).GetBytes("# Copyright © 2026\nWrite-Host 'ok'"));
        await File.WriteAllTextAsync(readablePath, "class Readable {}");
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var result = await scanner.ScanAsync(new(root, new ScanOptions(false, false)), new Progress<ScanProgress>(), CancellationToken.None);

            Assert.Contains("legacy.ps1", result.Files);
            Assert.Contains("readable.cs", result.Files);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(CommentSyntax.None)]
    [InlineData(CommentSyntax.CLike)]
    [InlineData(CommentSyntax.Batch)]
    public void AnalyzerHonorsCancellation(CommentSyntax syntax)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new CodeAnalyzer().Analyze("class Demo {}", syntax, cancellation.Token));
    }

    [Fact]
    public async Task ScannerStopsWithoutReturningPartialResultsWhenCancelled()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "large.js"), "/*" + new string('x', 8_000_000) + "*/");
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var task = scanner.ScanAsync(new(root, new ScanOptions(false, false)), new Progress<ScanProgress>(), cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(task.IsCanceled);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ProjectConfigurationStoreRoundTripsAllScanOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, ".projectscope.json");
        var configuration = new ProjectConfiguration(
            root,
            IncludeAssets: false,
            IncludeHidden: true,
            ExcludeBlankLinesAndComments: false,
            IgnoredDirectoryNames: new[] { "bin", "obj" },
            IgnoredPaths: new[] { "/cache/", @"regex:\.generated\.cs$" });
        var store = new ProjectConfigurationStore();
        try
        {
            await store.SaveAsync(path, configuration);
            var loaded = await store.LoadAsync(path);

            Assert.Equal(configuration.RootPath, loaded.RootPath);
            Assert.Equal(configuration.IncludeAssets, loaded.IncludeAssets);
            Assert.Equal(configuration.IncludeHidden, loaded.IncludeHidden);
            Assert.Equal(configuration.ExcludeBlankLinesAndComments, loaded.ExcludeBlankLinesAndComments);
            Assert.Equal(configuration.IgnoredDirectoryNames, loaded.IgnoredDirectoryNames);
            Assert.Equal(configuration.IgnoredPaths, loaded.IgnoredPaths);
            Assert.Contains("\"IncludeAssets\": false", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReportTotalsDoNotDoubleCountAssetFiles()
    {
        var result = new ScanResult("/demo", 10, Array.Empty<TypeCount>(), Array.Empty<CodeTypeCount>(), Array.Empty<AssetTypeCount>(), 0, 0, 0, 3, 128, Array.Empty<string>(), "Demo/\n", Array.Empty<DetailCount>(), Array.Empty<DetailCount>());
        var exporter = new ReportExporter();
        var json = exporter.Render(result, ReportFormat.Json);
        var html = exporter.Render(result, ReportFormat.Html);

        Assert.Contains("\"all_files\": 10", json);
        Assert.Contains("\"total_files\":10", html);
    }

    [Fact]
    public void ReportExportsSelfContainedHtmlAndMachineReadableJson()
    {
        var result = new ScanResult("/demo", 0, Array.Empty<TypeCount>(), Array.Empty<CodeTypeCount>(), Array.Empty<AssetTypeCount>(), 0, 0, 0, 0, 0, Array.Empty<string>(), "Demo/\n", Array.Empty<DetailCount>(), Array.Empty<DetailCount>());
        var exporter = new ReportExporter();
        Assert.Contains("/demo", exporter.Render(result, ReportFormat.Json));
        var html = exporter.Render(result, ReportFormat.Html);
        Assert.Contains("<!doctype html>", html);
        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        Assert.Contains("echarts.init", html);
        Assert.Contains("Demo/", exporter.Render(result, ReportFormat.DirectoryTree));
    }
}
