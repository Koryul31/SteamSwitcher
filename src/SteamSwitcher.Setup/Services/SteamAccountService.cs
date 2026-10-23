using System.Diagnostics;
using System.IO;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed class SteamAccountService
{
    public void OpenLogin(SteamInstallationInfo installation, SteamAccountInfo? account)
    {
        if (!File.Exists(installation.ExePath))
        {
            throw new FileNotFoundException(LocalizationService.T("StatusSteamNotFound"), installation.ExePath);
        }

        var arguments = !string.IsNullOrWhiteSpace(account?.AccountName)
            ? $"-login \"{account.AccountName}\""
            : string.Empty;

        Process.Start(new ProcessStartInfo
        {
            FileName = installation.ExePath,
            Arguments = arguments,
            WorkingDirectory = installation.DirectoryPath,
            UseShellExecute = true
        });
    }

    public void OpenFolder(SteamInstallationInfo installation)
    {
        if (Directory.Exists(installation.DirectoryPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{installation.DirectoryPath}\"",
                UseShellExecute = true
            });
        }
    }
}
