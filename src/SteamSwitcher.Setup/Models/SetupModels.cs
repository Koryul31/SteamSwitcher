using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SteamSwitcher.Setup.Models;
using SteamSwitcher.Setup.Services;

namespace SteamSwitcher.Setup.Models;

public sealed record SteamAccountInfo(
    string AccountName,
    string PersonaName,
    bool RememberPassword,
    bool MostRecent)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(PersonaName) || PersonaName.Equals(AccountName, StringComparison.OrdinalIgnoreCase)
            ? AccountName
            : $"{PersonaName} · {AccountName}";

    public string Label => DisplayName;

    public string RememberLabel => RememberPassword
        ? "Recordar contraseña activo"
        : "Recordar contraseña desactivado";

    public override string ToString() => DisplayName;
}

public sealed record SteamInstallationInfo(string ExePath)
{
    public string DirectoryPath => Path.GetDirectoryName(ExePath) ?? string.Empty;
}

public enum ShortcutMode
{
    None,
    Normal,
    BigPicture,
    Both
}

public enum ShortcutLayoutMode
{
    Folder,
    Direct
}

public sealed partial class ShortcutModeOption : ObservableObject
{
    public ShortcutModeOption(ShortcutMode value, string label)
    {
        Value = value;
        _label = label;
    }

    public ShortcutMode Value { get; }

    [ObservableProperty]
    private string _label;

    public override string ToString() => Label;
}

public sealed partial class ShortcutLayoutOption : ObservableObject
{
    public ShortcutLayoutOption(ShortcutLayoutMode value, string label)
    {
        Value = value;
        _label = label;
    }

    public ShortcutLayoutMode Value { get; }

    [ObservableProperty]
    private string _label;

    public override string ToString() => Label;
}

public sealed partial class LanguageOption : ObservableObject
{
    public LanguageOption(string value, string label)
    {
        Value = value;
        _label = label;
    }

    public string Value { get; }

    [ObservableProperty]
    private string _label;

    public override string ToString() => Label;
}

public sealed partial class ThemeOption : ObservableObject
{
    public ThemeOption(SetupThemeMode value, string label)
    {
        Value = value;
        _label = label;
    }

    public SetupThemeMode Value { get; }

    [ObservableProperty]
    private string _label;

    public override string ToString() => Label;
}

public sealed class BackendProfile
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
}

public sealed record InstallResult(bool Success, string? ExePath, string Message);
