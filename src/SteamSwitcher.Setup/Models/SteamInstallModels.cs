namespace SteamSwitcher.Setup.Models;

public enum SteamInstallStage
{
    Preparing,
    Downloading,
    VerifyingInstaller,
    ClosingSteam,
    Installing,
    VerifyingInstall,
    Completed,
    Failed
}

public sealed record SteamInstallTarget(
    string Slot,
    string Path,
    bool AlreadyInstalled);

public sealed record SteamInstallTargetResult(
    string Slot,
    string Path,
    bool Success,
    string? ExePath,
    string Message);

public sealed record SteamInstallProgress(
    SteamInstallStage Stage,
    string Message,
    string? Slot = null,
    double? Fraction = null,
    int CompletedTargets = 0,
    int TotalTargets = 0);

public sealed record SteamInstallResult(
    bool Success,
    string Message,
    IReadOnlyList<SteamInstallTargetResult> Targets)
{
    public string? ExePathFor(string slot) =>
        Targets.FirstOrDefault(target =>
            target.Success &&
            target.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase))?.ExePath;
}
