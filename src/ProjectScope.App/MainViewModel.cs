using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectScope.Core;
using ProjectScope.Infrastructure;

namespace ProjectScope.App;

public enum ScanState { Ready, Scanning, Completed, Cancelled, Failed }
public enum ThemePreference { System, Light, Dark, Cyberpunk }

public sealed record ThemeChoice(ThemePreference Value, string Label);

public static class ThemePreferenceValues
{
    public static ThemeChoice System { get; } = new(ThemePreference.System, "跟随系统");
    public static ThemeChoice Light { get; } = new(ThemePreference.Light, "浅色");
    public static ThemeChoice Dark { get; } = new(ThemePreference.Dark, "深色");
    public static ThemeChoice Cyberpunk { get; } = new(ThemePreference.Cyberpunk, "赛博朋克");

    public static IReadOnlyList<ThemeChoice> All { get; } = new[] { System, Light, Dark, Cyberpunk };
}

public partial class IgnoreDirectoryChoice : ObservableObject
{
    public IgnoreDirectoryChoice(string name) => Name = name;
    public string Name { get; }
    [ObservableProperty] private bool _isSelected = true;
}

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectScanner _scanner;
    private readonly ReportExporter _exporter;
    private readonly Action<ThemePreference> _applyTheme;
    private ScanResult? _result;
    private int _themeAnimationVersion;

    public MainViewModel(IProjectScanner scanner, ReportExporter exporter, ThemeChoice themeChoice, Action<ThemePreference> applyTheme)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(applyTheme);
        _scanner = scanner;
        _exporter = exporter;
        _applyTheme = applyTheme;
        _themePreference = themeChoice;
    }

    public IReadOnlyList<IgnoreDirectoryChoice> IgnoreDirectories { get; } = new[]
    {
        "node_modules", "bin", "obj", "build", "dist", "out", "target", "coverage",
        ".dart_tool", ".fvm", ".gradle", ".next", ".nuxt", ".cache",
        "__pycache__", ".pytest_cache", ".venv", "venv", "Pods", "DerivedData"
    }.Select(name => new IgnoreDirectoryChoice(name)).ToArray();

    [ObservableProperty] private string _customIgnoredPaths = "";
    [ObservableProperty] private string _rootPath = Environment.CurrentDirectory;
    [ObservableProperty] private bool _includeAssets = true;
    [ObservableProperty] private bool _includeHidden;
    [ObservableProperty] private bool _excludeBlankLinesAndComments = true;
    [ObservableProperty] private ThemeChoice _themePreference;
    [ObservableProperty] private bool _themeMenuOpen;
    [ObservableProperty] private double _themeMenuArrowAngle;
    [ObservableProperty] private bool _themeMenuClosing;
    [ObservableProperty] private double _themeMenuOpacity;
    [ObservableProperty] private double _themeMenuOffset = -6;
    [ObservableProperty] private string _themeStatus = "跟随系统";
    [ObservableProperty] private ScanState _state;
    [ObservableProperty] private string _status = "选择一个项目，开始了解它的规模。";
    [ObservableProperty] private string _summary = "尚未扫描";
    [ObservableProperty] private string _report = "扫描完成后，在这里查看统计报告。";
    [ObservableProperty] private string _directoryTree = "";
    [ObservableProperty] private string _fileList = "";
    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _log = "";
    [ObservableProperty] private bool _hasResult;
    public bool CanConfigure => State != ScanState.Scanning;

    [RelayCommand]
    private async Task ToggleThemeMenuAsync()
    {
        if (ThemeMenuClosing) return;
        if (ThemeMenuOpen)
        {
            ThemeMenuClosing = true;
            await AnimateThemeMenuAsync(1, 0, 0, -6, 0);
            ThemeMenuOpen = false;
            ThemeMenuArrowAngle = 0;
            ThemeMenuClosing = false;
        }
        else
        {
            ThemeMenuOpacity = 0;
            ThemeMenuOffset = -6;
            ThemeMenuOpen = true;
            await AnimateThemeMenuAsync(0, -6, 1, 0, 180);
        }
    }

    [RelayCommand]
    private async Task SelectThemeAsync(ThemeChoice choice)
    {
        if (ThemeMenuClosing) return;
        ArgumentNullException.ThrowIfNull(choice);
        ThemePreference = choice;
        ThemeMenuClosing = true;
        await AnimateThemeMenuAsync(1, 0, 0, -6, 0);
        ThemeMenuOpen = false;
        ThemeMenuClosing = false;
    }

    public async Task CloseThemeMenuAsync()
    {
        if (!ThemeMenuOpen || ThemeMenuClosing) return;
        ThemeMenuClosing = true;
        await AnimateThemeMenuAsync(ThemeMenuOpacity, ThemeMenuOffset, 0, -6, 0);
        ThemeMenuOpen = false;
        ThemeMenuClosing = false;
    }

    private async Task AnimateThemeMenuAsync(double fromOpacity, double fromOffset, double toOpacity, double toOffset, double toArrowAngle)
    {
        var fromAngle = ThemeMenuArrowAngle;
        var version = ++_themeAnimationVersion;
        fromOpacity = ThemeMenuOpacity;
        fromOffset = ThemeMenuOffset;
        const int frames = 10;
        for (var frame = 1; frame <= frames; frame++)
        {
            await Task.Delay(16);
            if (version != _themeAnimationVersion) return;
            var progress = 1 - Math.Pow(1 - (double)frame / frames, 3);
            ThemeMenuOpacity = fromOpacity + ((toOpacity - fromOpacity) * progress);
            ThemeMenuArrowAngle = fromAngle + ((toArrowAngle - fromAngle) * progress);
        }
    }

    partial void OnThemePreferenceChanged(ThemeChoice value)
    {
        ThemeStatus = value.Label;
        _applyTheme(value.Value);
    }

    partial void OnStateChanged(ScanState value)
    {
        OnPropertyChanged(nameof(CanConfigure));
        ScanCommand.NotifyCanExecuteChanged();
    }

    private bool CanScan() => CanConfigure;

    [RelayCommand(CanExecute = nameof(CanScan), IncludeCancelCommand = true)]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        _result = null;
        HasResult = false;
        Report = "";
        DirectoryTree = "";
        FileList = "";
        Details = "";
        Summary = "扫描中";
        Log = "";
        State = ScanState.Scanning;
        try
        {
            var options = new ScanOptions(IncludeAssets, IncludeHidden)
            {
                IgnoredDirectoryNames = IgnoreDirectories.Where(item => item.IsSelected).Select(item => item.Name).ToArray(),
                IgnoredPaths = CustomIgnoredPaths.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                ExcludeBlankLinesAndComments = ExcludeBlankLinesAndComments
            };
            var request = new ScanRequest(RootPath, options);
            Status = "正在扫描…";
            var progress = new Progress<ScanProgress>(item =>
            {
                if (State != ScanState.Scanning) return;
                Status = $"已处理 {item.ProcessedFiles:N0} 个文件 · {item.CurrentPath}";
                AppendLog(Status);
            });
            var result = await _scanner.ScanAsync(request, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _result = result;
            Report = _exporter.Render(result, ReportFormat.Text);
            DirectoryTree = result.DirectoryTree;
            FileList = string.Join(Environment.NewLine, result.Files);
            Details = string.Join(Environment.NewLine, result.Details.Concat(result.AssetDetails).Select(item => $"{item.Category} / {item.Type} / {item.Extension} : {item.Count:N0}"));
            Summary = $"{result.TotalFiles:N0} 个文件     {result.TotalCodeLines:N0} 行代码     {result.TotalCodeCharacters:N0} 字符     {ReportExporter.FormatBytes(result.TotalAssetBytes)} 资源";
            State = ScanState.Completed;
            HasResult = true;
            Status = "扫描完成";
            AppendLog(Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = ScanState.Cancelled;
            Summary = "扫描已取消";
            Status = "扫描已取消；未生成统计结果。";
            AppendLog(Status);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            State = ScanState.Failed;
            Summary = "扫描失败";
            Status = exception.Message;
            AppendLog(exception.ToString());
        }
    }

    public string GetReport(ReportFormat format) => _exporter.Render(RequireResult(), format);
    public Task ExportAsync(ReportFormat format, string path) => _exporter.ExportAsync(RequireResult(), format, path, CancellationToken.None);

    public void ReportOperationError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Status = exception.Message;
        AppendLog(exception.ToString());
    }

    private ScanResult RequireResult()
    {
        if (_result is null) throw new InvalidOperationException("没有完整的扫描结果，请先完成一次扫描。");
        return _result;
    }

    private void AppendLog(string message)
    {
        Log += message + Environment.NewLine;
        if (Log.Length > 64000) Log = Log[^32000..];
    }
}
