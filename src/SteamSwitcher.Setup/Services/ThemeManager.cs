using System.Windows;
using Microsoft.Win32;

namespace SteamSwitcher.Setup.Services;

public enum SetupThemeMode
{
    System,
    Light,
    Dark
}

public static class ThemeManager
{
    private static readonly Uri LightThemeUri =
        new("pack://application:,,,/SteamSwitcher.Setup;component/Themes/Light.xaml", UriKind.Absolute);

    private static readonly Uri DarkThemeUri =
        new("pack://application:,,,/SteamSwitcher.Setup;component/Themes/Dark.xaml", UriKind.Absolute);

    private static bool _watchingSystem;
    private static bool _lastSystemDark;

    public static SetupThemeMode CurrentMode { get; private set; } = SetupThemeMode.System;
    public static bool IsDark { get; private set; }

    public static event Action<SetupThemeMode>? ThemeChanged;

    public static void Apply(SetupThemeMode mode)
    {
        CurrentMode = mode;
        IsDark = mode == SetupThemeMode.Dark ||
                 (mode == SetupThemeMode.System && IsSystemDark());

        var application = Application.Current;
        if (application is null)
        {
            return;
        }

#pragma warning disable WPF0001
        application.ThemeMode = IsDark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001

        var dictionaries = application.Resources.MergedDictionaries;
        var theme = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) == true ||
            dictionary.Source?.OriginalString.Contains("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase) == true);

        var replacement = new ResourceDictionary { Source = IsDark ? DarkThemeUri : LightThemeUri };
        if (theme is null)
        {
            dictionaries.Add(replacement);
        }
        else
        {
            var index = dictionaries.IndexOf(theme);
            dictionaries.RemoveAt(index);
            dictionaries.Insert(index, replacement);
        }

        EnsureSystemWatcher();
        _lastSystemDark = IsSystemDark();
        ThemeChanged?.Invoke(mode);
    }

    public static void RefreshSystemTheme()
    {
        if (CurrentMode != SetupThemeMode.System)
        {
            return;
        }

        var dark = IsSystemDark();
        if (dark != _lastSystemDark || dark != IsDark)
        {
            Apply(SetupThemeMode.System);
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return value is int integer && integer == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSystemWatcher()
    {
        if (_watchingSystem)
        {
            return;
        }

        try
        {
            SystemEvents.UserPreferenceChanged += (_, _) =>
            {
                if (CurrentMode != SetupThemeMode.System)
                {
                    return;
                }

                Application.Current?.Dispatcher.BeginInvoke(() => RefreshSystemTheme());
            };
            _watchingSystem = true;
        }
        catch
        {
            // If the system does not expose preference events, keep the current theme.
        }
    }
}
