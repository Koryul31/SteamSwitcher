using System.Diagnostics;
using System.Windows.Forms;
using SteamSwitcher.Core.Models;
using SteamSwitcher.Core.Services;

namespace SteamSwitcher.Cli;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        string? accountName = null;
        string? customDirectory = null;
        string? customArgs = null;
        bool bigPicture = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if ((arg.Equals("--account", StringComparison.OrdinalIgnoreCase) || arg.Equals("-a", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                accountName = args[++i];
            }
            else if (arg.StartsWith("--account=", StringComparison.OrdinalIgnoreCase))
            {
                accountName = arg["--account=".Length..];
            }
            else if ((arg.Equals("--dir", StringComparison.OrdinalIgnoreCase) || arg.Equals("-d", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                customDirectory = args[++i];
            }
            else if (arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
            {
                customDirectory = arg["--dir=".Length..];
            }
            else if (arg.Equals("--bigpicture", StringComparison.OrdinalIgnoreCase) || arg.Equals("-bp", StringComparison.OrdinalIgnoreCase))
            {
                bigPicture = true;
            }
            else if (arg.Equals("--args", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                customArgs = args[++i];
            }
            else if (!arg.StartsWith("-") && string.IsNullOrWhiteSpace(accountName))
            {
                accountName = arg;
            }
            else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("/?"))
            {
                MessageBox.Show(
                    "SteamSwitcher 2.0\n\n" +
                    "Usage:\n" +
                    "  SteamSwitcher.exe --account <accountName> [--bigpicture] [--args <launchArgs>]\n" +
                    "  SteamSwitcher.exe --dir <steamDirectory> [--account <accountName>] [--bigpicture]\n\n" +
                    "Options:\n" +
                    "  --account, -a    Target Steam account name to switch to\n" +
                    "  --dir, -d        Physical Steam directory (Multi-Install mode)\n" +
                    "  --bigpicture, -bp Launch directly in Big Picture Mode\n" +
                    "  --args           Additional command line arguments passed to Steam\n",
                    "SteamSwitcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
        }

        // If run with no parameters, try opening the Setup GUI
        if (string.IsNullOrWhiteSpace(accountName) && string.IsNullOrWhiteSpace(customDirectory))
        {
            var setupExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamSwitcher.Setup.exe");
            if (File.Exists(setupExe))
            {
                Process.Start(new ProcessStartInfo { FileName = setupExe, UseShellExecute = true });
                return 0;
            }

            MessageBox.Show(
                "Please specify an account (--account <name>) or installation directory (--dir <path>).\n\n" +
                "Run SteamSwitcher.exe --help for more information.",
                "SteamSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return 1;
        }

        // Check if accountName refers to a profile key in settings.ini (e.g. steam1 or steam2)
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var settingsIni = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            if (File.Exists(settingsIni))
            {
                var lines = File.ReadAllLines(settingsIni);
                string? currentSection = null;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed[1..^1].Trim();
                        continue;
                    }
                    if (currentSection != null && (currentSection.Equals($"Profile:{accountName}", StringComparison.OrdinalIgnoreCase) || currentSection.Equals(accountName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var parts = trimmed.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var k = parts[0].Trim();
                            var v = parts[1].Trim();
                            if (k.Equals("Login", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(v))
                            {
                                accountName = v;
                            }
                            if (k.Equals("ExePath", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(customDirectory) && File.Exists(v))
                            {
                                customDirectory = Path.GetDirectoryName(v);
                            }
                        }
                    }
                }
            }
        }

        var discovery = new SteamDiscoveryService();
        var accountService = new SteamAccountService();
        var registry = new RegistryService();
        var processService = new SteamProcessService();
        var switchService = new SteamSwitchService(discovery, accountService, registry, processService);

        var mode = !string.IsNullOrWhiteSpace(customDirectory)
            ? ProfileMode.MultiInstall
            : ProfileMode.SingleInstall;

        var profile = new SteamProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = accountName ?? customDirectory ?? "Steam",
            Mode = mode,
            AccountName = accountName,
            CustomInstallDirectory = customDirectory,
            CustomLaunchArguments = customArgs
        };

        var success = await switchService.SwitchAsync(profile, bigPicture);
        if (!success)
        {
            MessageBox.Show(
                $"Failed to switch to Steam profile '{profile.DisplayName}'.\n\n" +
                "Please ensure Steam is installed and not blocked by another application.",
                "SteamSwitcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        return 0;
    }
}
