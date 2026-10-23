namespace SteamSwitcher.Core.Models;

public sealed record SteamInstallation
{
    public required string DirectoryPath { get; init; }
    public string ExePath => Path.Combine(DirectoryPath, "steam.exe");
    public bool Exists => File.Exists(ExePath);
    public string ConfigDirectory => Path.Combine(DirectoryPath, "config");
    public string LoginUsersPath => Path.Combine(ConfigDirectory, "loginusers.vdf");
    public string AvatarCacheDirectory => Path.Combine(ConfigDirectory, "avatarcache");
    public string UserDataDirectory => Path.Combine(DirectoryPath, "userdata");
}
