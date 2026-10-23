using SteamSwitcher.Core.Models;

namespace SteamSwitcher.Core.Services;

public interface ISteamSwitchService
{
    Task<bool> SwitchAsync(SteamProfile profile, bool bigPicture = false, CancellationToken cancellationToken = default);
}

public sealed class SteamSwitchService : ISteamSwitchService
{
    private readonly ISteamDiscoveryService _discovery;
    private readonly ISteamAccountService _accountService;
    private readonly IRegistryService _registry;
    private readonly ISteamProcessService _processService;

    public SteamSwitchService(
        ISteamDiscoveryService discovery,
        ISteamAccountService accountService,
        IRegistryService registry,
        ISteamProcessService processService)
    {
        _discovery = discovery;
        _accountService = accountService;
        _registry = registry;
        _processService = processService;
    }

    public async Task<bool> SwitchAsync(SteamProfile profile, bool bigPicture = false, CancellationToken cancellationToken = default)
    {
        if (profile.Mode == ProfileMode.SingleInstall)
        {
            return await SwitchSingleInstallAsync(profile, bigPicture, cancellationToken);
        }
        else
        {
            return await SwitchMultiInstallAsync(profile, bigPicture, cancellationToken);
        }
    }

    private async Task<bool> SwitchSingleInstallAsync(SteamProfile profile, bool bigPicture, CancellationToken cancellationToken)
    {
        var installation = _discovery.GetPrimaryInstallation();
        if (installation == null || !installation.Exists)
        {
            return false;
        }

        var isRunning = _processService.IsSteamRunning();
        var currentAutoLogin = _registry.GetAutoLoginUser();

        // If target account is already the one logged in and Steam is running, just focus it
        if (isRunning && !string.IsNullOrWhiteSpace(profile.AccountName) &&
            string.Equals(currentAutoLogin, profile.AccountName, StringComparison.OrdinalIgnoreCase))
        {
            if (bigPicture)
            {
                // In modern Steam, launching with -bigpicture while running focuses Big Picture
                _processService.LaunchSteam(installation.ExePath, "-bigpicture");
            }
            else
            {
                _processService.ActivateSteamWindow(installation.ExePath);
            }
            return true;
        }

        // Otherwise, gracefully stop running Steam first
        if (isRunning)
        {
            var stopped = await _processService.ShutdownSteamAsync(
                installation.ExePath,
                TimeSpan.FromSeconds(25),
                cancellationToken);

            if (!stopped)
            {
                return false;
            }
        }

        // Set registry and VDF
        if (!string.IsNullOrWhiteSpace(profile.AccountName))
        {
            _registry.SetAutoLoginUser(profile.AccountName);
            _accountService.SetActiveAccount(installation, profile.AccountName);
        }

        var arguments = bigPicture ? "-bigpicture" : string.Empty;
        if (!string.IsNullOrWhiteSpace(profile.CustomLaunchArguments))
        {
            arguments = $"{arguments} {profile.CustomLaunchArguments}".Trim();
        }

        return _processService.LaunchSteam(installation.ExePath, arguments);
    }

    private async Task<bool> SwitchMultiInstallAsync(SteamProfile profile, bool bigPicture, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.CustomInstallDirectory))
        {
            return false;
        }

        var targetInstallation = new SteamInstallation { DirectoryPath = profile.CustomInstallDirectory };
        if (!targetInstallation.Exists)
        {
            return false;
        }

        // 1. Gracefully shutdown any running Steam
        if (_processService.IsSteamRunning())
        {
            // Use currently registered SteamExe or target to send shutdown
            var currentPath = _registry.GetCurrentSteamPath();
            var shutdownExe = !string.IsNullOrWhiteSpace(currentPath)
                ? Path.Combine(currentPath.Replace('/', '\\'), "steam.exe")
                : targetInstallation.ExePath;

            var stopped = await _processService.ShutdownSteamAsync(
                shutdownExe,
                TimeSpan.FromSeconds(25),
                cancellationToken);

            if (!stopped)
            {
                return false;
            }
        }

        // 2. CRITICAL POINT 5: Atomically swap Windows registry and steam:// protocol pointers
        _registry.SetActiveInstallation(profile.CustomInstallDirectory);

        // 3. Update AutoLoginUser and VDF if account specified
        if (!string.IsNullOrWhiteSpace(profile.AccountName))
        {
            _registry.SetAutoLoginUser(profile.AccountName);
            _accountService.SetActiveAccount(targetInstallation, profile.AccountName);
        }

        // 4. Launch target Steam
        var arguments = bigPicture ? "-bigpicture" : string.Empty;
        if (!string.IsNullOrWhiteSpace(profile.CustomLaunchArguments))
        {
            arguments = $"{arguments} {profile.CustomLaunchArguments}".Trim();
        }

        return _processService.LaunchSteam(targetInstallation.ExePath, arguments);
    }
}
