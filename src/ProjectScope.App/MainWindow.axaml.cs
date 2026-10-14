using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using ProjectScope.Core;
using ProjectScope.Infrastructure;

namespace ProjectScope.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow() : this(CreateViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Closing += (_, _) => viewModel.ScanCommand.Cancel();
        AddHandler(InputElement.PointerPressedEvent, OnThemeOutsidePressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnThemeKeyDown, RoutingStrategies.Tunnel);
        Deactivated += async (_, _) => await viewModel.CloseThemeMenuAsync();
    }

    private async void OnThemeOutsidePressed(object? sender, PointerPressedEventArgs args)
    {
        if (!_viewModel.ThemeMenuOpen) return;
        if (args.Source is Avalonia.Visual source &&
            (ReferenceEquals(source, ThemeMenuPanel) || ThemeMenuPanel.IsVisualAncestorOf(source) ||
             ReferenceEquals(source, ThemeMenuButton) || ThemeMenuButton.IsVisualAncestorOf(source))) return;

        await _viewModel.CloseThemeMenuAsync();
    }

    private async void OnThemeKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || !_viewModel.ThemeMenuOpen) return;
        args.Handled = true;
        await _viewModel.CloseThemeMenuAsync();
        ThemeMenuButton.Focus();
    }

    private static MainViewModel CreateViewModel()
    {
        var catalog = new FileCatalog();
        return new MainViewModel(new ProjectScanner(catalog, new CodeAnalyzer()), new ReportExporter(), ThemePreferenceValues.All[0], static _ => { });
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs args)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "选择项目根目录", AllowMultiple = false });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("所选目录不是可扫描的本地文件系统路径。");
        _viewModel.RootPath = path;
    }

    private async void LoadConfigurationClicked(object? sender, RoutedEventArgs args)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "加载扫描配置",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("ProjectScope 配置") { Patterns = new[] { ".projectscope.json", "*.json" } } }
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("所选配置不是本地文件系统路径。");
        try
        {
            await _viewModel.LoadConfigurationAsync(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {
            _viewModel.ReportOperationError(exception);
        }
    }

    private void PortfolioClicked(object? sender, RoutedEventArgs args)
    {
        var process = Process.Start(new ProcessStartInfo("https://hatsusumi.github.io/FinalTestamentProofILived/") { UseShellExecute = true });
        if (process is not null) process.Dispose();

    }

    private async void CopyTreeClicked(object? sender, RoutedEventArgs args)
    {
        if (Clipboard is null) throw new InvalidOperationException("桌面平台未提供剪贴板服务。");
        await Clipboard.SetTextAsync(_viewModel.GetReport(ReportFormat.DirectoryTree));
    }

    private async void ExportClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string formatName }) throw new InvalidOperationException("导出按钮缺少报告格式配置。");
        var format = Enum.Parse<ReportFormat>(formatName);
        var extension = format switch
        {
            ReportFormat.Markdown => "md",
            ReportFormat.Json => "json",
            ReportFormat.Html => "html",
            ReportFormat.Text or ReportFormat.DirectoryTree => "txt",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存项目统计", SuggestedFileName = "project-report." + extension, DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(formatName) { Patterns = new[] { "*." + extension } } }
        });
        if (file is null) return;
        var path = file.TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("导出目标不是本地文件系统路径。");
        try
        {
            await _viewModel.ExportAsync(format, path);
            if (format == ReportFormat.Html)
            {
                var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                if (process is not null) process.Dispose();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _viewModel.ReportOperationError(exception);
        }
    }
}
