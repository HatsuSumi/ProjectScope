using ProjectScope.Core;
using ProjectScope.Infrastructure;

namespace ProjectScope.Tests;

public sealed class StatisticsTests
{
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
        Assert.Equal("WebAssembly", catalog.DetectTextType("module.wat").Name);
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
    public async Task ScannerCountsEachFileOnceAndUsesConfiguredFilters()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "main.cs"), "class Demo { } // 注释\n");
        await File.WriteAllTextAsync(Path.Combine(root, "model3.json"), "{\"ok\": true}");
        await File.WriteAllBytesAsync(Path.Combine(root, "icon.png"), new byte[] { 0x89, 0x50, 0x4e, 0x47 });
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "ignored.js"), "const ignored = true;");
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var options = new ScanOptions(true, false, false, true, true);
            var result = await scanner.ScanAsync(new(root, options), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Equal(3, result.TotalFiles);
            Assert.Equal(1, result.TotalAssetFiles);
            Assert.Equal(2, result.TotalCodeFiles);
            Assert.Equal(3, result.Files.Count);
            Assert.Contains("main.cs", result.DirectoryTree);
            Assert.DoesNotContain("ignored.js", result.DirectoryTree);
            Assert.Contains(result.AssetTypes, item => item.Key == "image" && item.Files == 1);
            Assert.Contains(result.Details, item => item.Category == "file" && item.Extension == ".cs");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerHonorsFileListAndDetailSwitches()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "main.js"), "const value = 1;");
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var result = await scanner.ScanAsync(new(root, new ScanOptions(false, false, false, false, false)), new Progress<ScanProgress>(), CancellationToken.None);
            Assert.Empty(result.Files);
            Assert.Empty(result.Details);
            Assert.Contains("main.js", result.DirectoryTree);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ScannerRejectsInvalidUtf8InsteadOfReturningPartialStatistics()
    {
        var root = Path.Combine(Path.GetTempPath(), "ProjectScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, "invalid.cs"), new byte[] { 0xff, 0xfe, 0xfd });
        try
        {
            var scanner = new ProjectScanner(new FileCatalog(), new CodeAnalyzer());
            var exception = await Assert.ThrowsAsync<IOException>(() => scanner.ScanAsync(new(root, new ScanOptions(false, false, false, false, false)), new Progress<ScanProgress>(), CancellationToken.None));
            Assert.Contains("invalid.cs", exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReportExportsSelfContainedHtmlAndMachineReadableJson()
    {
        var result = new ScanResult("/demo", 0, Array.Empty<TypeCount>(), Array.Empty<CodeTypeCount>(), Array.Empty<AssetTypeCount>(), 0, 0, 0, 0, 0, Array.Empty<string>(), "Demo/\n", Array.Empty<DetailCount>(), Array.Empty<DetailCount>());
        var exporter = new ReportExporter();
        Assert.Contains("/demo", exporter.Render(result, ReportFormat.Json));
        Assert.Contains("<!doctype html>", exporter.Render(result, ReportFormat.Html));
        Assert.Contains("Demo/", exporter.Render(result, ReportFormat.DirectoryTree));
    }
}
