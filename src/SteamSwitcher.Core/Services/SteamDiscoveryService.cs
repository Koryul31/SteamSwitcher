using Microsoft.Win32;
using SteamSwitcher.Core.Models;

namespace SteamSwitcher.Core.Services;

public interface ISteamDiscoveryService
{
    SteamInstallation? GetPrimaryInstallation();
    IReadOnlyList<SteamInstallation> FindAllInstallations();
}

public sealed class SteamDiscoveryService : ISteamDiscoveryService
{
    public SteamInstallation? GetPrimaryInstallation()
    {
        var candidates = FindAllInstallations();
        return candidates.FirstOrDefault(i => i.Exists);
    }

    public IReadOnlyList<SteamInstallation> FindAllInstallations()
    {
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SteamInstallation>();

        void TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var clean = path.Trim().Trim('"', '\'').Replace('/', '\\');
            string dir;

            if (File.Exists(clean) && Path.GetFileName(clean).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                dir = Path.GetDirectoryName(clean) ?? string.Empty;
            }
            else if (Directory.Exists(clean))
            {
                dir = clean;
            }
            else
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(dir)) return;

            try
            {
                var full = Path.GetFullPath(dir).TrimEnd('\\');
                var exe = Path.Combine(full, "steam.exe");
                if (File.Exists(exe) && foundPaths.Add(full))
                {
                    results.Add(new SteamInstallation { DirectoryPath = full });
                }
            }
            catch
            {
                // Invalid path format
            }
        }

        // 1. Registry (Current User)
        try
        {
            using var cuKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (cuKey != null)
            {
                TryAdd(cuKey.GetValue("SteamPath") as string);
                TryAdd(cuKey.GetValue("SteamExe") as string);
            }
        }
        catch { }

        // 2. Registry (Local Machine 64-bit and 32-bit)
        try
        {
            using var lmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                           ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (lmKey != null)
            {
                TryAdd(lmKey.GetValue("InstallPath") as string);
            }
        }
        catch { }

        // 3. Default folder paths
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"C:\",
            @"D:\",
            @"E:\"
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            TryAdd(Path.Combine(root, "Steam"));
            TryAdd(Path.Combine(root, "Steam2"));
            TryAdd(Path.Combine(root, "Steam_A"));
            TryAdd(Path.Combine(root, "Steam_B"));

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "Steam*", SearchOption.TopDirectoryOnly))
                {
                    TryAdd(dir);
                }
            }
            catch { }
        }

        return results;
    }
}
