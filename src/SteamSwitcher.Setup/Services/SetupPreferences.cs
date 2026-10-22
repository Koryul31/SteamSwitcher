using System.IO;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed class SetupPreferences
{
    public ShortcutMode ShortcutA { get; set; } = ShortcutMode.Both;
    public ShortcutMode ShortcutB { get; set; } = ShortcutMode.Both;
    public string ProfileAKey { get; set; } = string.Empty;
    public string ProfileBKey { get; set; } = string.Empty;
    public SetupThemeMode Theme { get; set; } = SetupThemeMode.System;
    public string Language { get; set; } = "System";
    public ShortcutLayoutMode ShortcutLayout { get; set; } = ShortcutLayoutMode.Folder;
    public bool CreateDesktop { get; set; } = true;
    public bool CreateStartMenu { get; set; } = true;
    public bool WelcomeCompleted { get; set; }
    public bool Exists { get; set; }

    public static SetupPreferences Load(SetupContext context)
    {
        var preferences = new SetupPreferences();
        if (!File.Exists(context.SetupPath))
        {
            return preferences;
        }

        var document = IniDocument.Load(context.SetupPath);
        preferences.Exists = true;
        preferences.ShortcutA = ParseMode(document.Get("Setup", "ShortcutA"));
        preferences.ShortcutB = ParseMode(document.Get("Setup", "ShortcutB"));
        preferences.ProfileAKey = document.Get("Setup", "ProfileA") ?? string.Empty;
        preferences.ProfileBKey = document.Get("Setup", "ProfileB") ?? string.Empty;
        preferences.Theme = ParseTheme(document.Get("Setup", "Theme"));
        preferences.Language = document.Get("Setup", "Language") ?? "System";
        preferences.ShortcutLayout = ParseLayout(document.Get("Setup", "ShortcutLayout"));
        preferences.CreateDesktop = ReadBool(document.Get("Setup", "CreateDesktop"), true);
        preferences.CreateStartMenu = ReadBool(document.Get("Setup", "CreateStartMenu"), true);
        preferences.WelcomeCompleted = ReadBool(document.Get("Setup", "WelcomeCompleted"), false);
        return preferences;
    }

    public void Save(SetupContext context)
    {
        var document = IniDocument.Load(context.SetupPath);
        document.Set("Setup", "ShortcutA", ShortcutA.ToString());
        document.Set("Setup", "ShortcutB", ShortcutB.ToString());
        document.Set("Setup", "ProfileA", ProfileAKey);
        document.Set("Setup", "ProfileB", ProfileBKey);
        document.Set("Setup", "Theme", Theme.ToString());
        document.Set("Setup", "Language", Language);
        document.Set("Setup", "ShortcutLayout", ShortcutLayout.ToString());
        document.Set("Setup", "CreateDesktop", CreateDesktop ? "true" : "false");
        document.Set("Setup", "CreateStartMenu", CreateStartMenu ? "true" : "false");
        document.Set("Setup", "WelcomeCompleted", WelcomeCompleted ? "true" : "false");
        document.Save(context.SetupPath);
    }

    public void MarkWelcomeCompleted(SetupContext context)
    {
        WelcomeCompleted = true;

        var document = IniDocument.Load(context.SetupPath);
        document.Set("Setup", "WelcomeCompleted", "true");
        document.Save(context.SetupPath);
        Exists = true;
    }

    private static ShortcutMode ParseMode(string? value)
    {
        return Enum.TryParse<ShortcutMode>(value, true, out var mode) ? mode : ShortcutMode.Both;
    }

    private static SetupThemeMode ParseTheme(string? value)
    {
        return Enum.TryParse<SetupThemeMode>(value, true, out var theme) ? theme : SetupThemeMode.System;
    }

    private static ShortcutLayoutMode ParseLayout(string? value)
    {
        return Enum.TryParse<ShortcutLayoutMode>(value, true, out var layout) ? layout : ShortcutLayoutMode.Folder;
    }

    private static bool ReadBool(string? value, bool fallback)
    {
        return value is null ? fallback : !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }
}
