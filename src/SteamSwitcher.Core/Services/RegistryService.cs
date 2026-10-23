using Microsoft.Win32;

namespace SteamSwitcher.Core.Services;

public interface IRegistryService
{
    string? GetAutoLoginUser();
    bool SetAutoLoginUser(string accountName);
    bool SetActiveInstallation(string steamDirectory);
    string? GetCurrentSteamPath();
}

public sealed class RegistryService : IRegistryService
{
    private const string SteamKeyPath = @"Software\Valve\Steam";
    private const string ProtocolKeyPath = @"Software\Classes\steam\shell\open\command";

    public string? GetAutoLoginUser()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SteamKeyPath);
            return key?.GetValue("AutoLoginUser") as string;
        }
        catch
        {
            return null;
        }
    }

    public bool SetAutoLoginUser(string accountName)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SteamKeyPath);
            if (key == null) return false;

            key.SetValue("AutoLoginUser", accountName, RegistryValueKind.String);
            key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetCurrentSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SteamKeyPath);
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    public bool SetActiveInstallation(string steamDirectory)
    {
        try
        {
            var fullDir = Path.GetFullPath(steamDirectory).TrimEnd('\\');
            var steamExe = Path.Combine(fullDir, "steam.exe");
            if (!File.Exists(steamExe))
            {
                return false;
            }

            // Steam uses forward slashes in SteamPath
            var forwardSlashPath = fullDir.Replace('\\', '/');

            // 1. HKCU\Software\Valve\Steam
            using (var key = Registry.CurrentUser.CreateSubKey(SteamKeyPath))
            {
                if (key != null)
                {
                    key.SetValue("SteamPath", forwardSlashPath, RegistryValueKind.String);
                    key.SetValue("SteamExe", steamExe.Replace('\\', '/'), RegistryValueKind.String);
                }
            }

            // 2. HKCU\Software\Classes\steam\shell\open\command
            using (var protoKey = Registry.CurrentUser.CreateSubKey(ProtocolKeyPath))
            {
                if (protoKey != null)
                {
                    protoKey.SetValue(string.Empty, $"\"{steamExe}\" -- \"%1\"", RegistryValueKind.String);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
