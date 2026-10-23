namespace SteamSwitcher.Core.Models;

public sealed record SteamAccount
{
    public required string SteamId64 { get; init; }
    public required string AccountName { get; init; }
    public string PersonaName { get; init; } = string.Empty;
    public bool RememberPassword { get; init; }
    public bool MostRecent { get; init; }
    public long Timestamp { get; init; }
    public string? AvatarPath { get; init; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(PersonaName)
            ? $"{PersonaName} ({AccountName})"
            : AccountName;
}
