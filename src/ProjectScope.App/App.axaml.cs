using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using ProjectScope.Core;
using ProjectScope.Infrastructure;

namespace ProjectScope.App;

public partial class App : Application
{
    public static readonly ThemeVariant CyberpunkTheme = new("Cyberpunk", ThemeVariant.Dark);

    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProjectScope",
        "preferences.json");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("ProjectScope requires the classic desktop Avalonia lifetime.");

        var catalog = new FileCatalog();
        var scanner = new ProjectScanner(catalog, new CodeAnalyzer());
        var themePreference = LoadThemePreference();
        ApplyTheme(themePreference);
        var viewModel = new MainViewModel(scanner, new ReportExporter(), ThemePreferenceValues.All.Single(choice => choice.Value == themePreference), ApplyTheme);
        desktop.MainWindow = new MainWindow(viewModel);
        base.OnFrameworkInitializationCompleted();
    }

    private static ThemePreference LoadThemePreference()
    {
        try
        {
            if (!File.Exists(PreferencesPath)) return ThemePreference.System;
            var preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath));
            return preferences?.Theme is ThemePreference theme ? theme : ThemePreference.System;
        }
        catch (JsonException)
        {
            return ThemePreference.System;
        }
    }

    private static void ApplyTheme(ThemePreference preference)
    {
        if (Current is null) throw new InvalidOperationException("The Avalonia application has not been initialized.");
        Current.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            ThemePreference.Cyberpunk => CyberpunkTheme,
            ThemePreference.System => ThemeVariant.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(preference))
        };
        SaveThemePreference(preference);
    }

    private static void SaveThemePreference(ThemePreference preference)
    {
        var directory = Path.GetDirectoryName(PreferencesPath)
            ?? throw new InvalidOperationException("Unable to determine the preferences directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(new Preferences(preference)));
    }

    private sealed record Preferences(ThemePreference Theme);
}
