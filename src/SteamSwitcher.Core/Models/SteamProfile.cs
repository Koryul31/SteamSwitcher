namespace SteamSwitcher.Core.Models;

public sealed record SteamProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; set; }
    public ProfileMode Mode { get; set; } = ProfileMode.SingleInstall;
    public string? AccountName { get; set; }
    public string? SteamId64 { get; set; }
    public string? CustomInstallDirectory { get; set; }
    public string? CustomLaunchArguments { get; set; }
    public string? CustomIconPath { get; set; }
}
