namespace SteamSwitcher.Core.Models;

public enum ProfileMode
{
    /// <summary>
    /// Switches accounts inside a single shared Steam installation.
    /// Fast, lightweight, no file duplication.
    /// </summary>
    SingleInstall = 0,

    /// <summary>
    /// Switches between separate physical Steam directory installations.
    /// Performs registry and protocol swapping to keep Windows integration intact.
    /// </summary>
    MultiInstall = 1
}

public enum ShortcutType
{
    Normal = 0,
    BigPicture = 1,
    Both = 2
}

[Flags]
public enum ShortcutTargetLocations
{
    None = 0,
    Desktop = 1,
    StartMenu = 2,
    DedicatedFolder = 4
}
