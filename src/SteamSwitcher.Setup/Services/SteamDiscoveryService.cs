using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed class SteamDiscoveryService
{
    public IReadOnlyList<SteamInstallationInfo> Discover(IEnumerable<string>? extraPaths = null)
    {
        var found = new List<string>();

        void AddCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var candidate = value.Trim().Trim('"').Replace('/', '\\');
            string? exe = null;
            if (File.Exists(candidate))
            {
                exe = candidate;
            }
            else if (Directory.Exists(candidate))
            {
                exe = Path.Combine(candidate, "steam.exe");
            }

            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe) ||
                !Path.GetFileName(exe).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullPath = Path.GetFullPath(exe);
            if (!found.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(fullPath);
            }
        }

        foreach (var (hive, path) in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
            (Registry.LocalMachine, @"SOFTWARE\Valve\Steam")
        })
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key is null)
                {
                    continue;
                }

                AddCandidate(key.GetValue("SteamExe") as string);
                AddCandidate(key.GetValue("SteamPath") as string);
                AddCandidate(key.GetValue("InstallPath") as string);
            }
            catch
            {
                // The registry may be inaccessible; continue with the usual paths.
            }
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            AddCandidate(Path.Combine(root, "Steam"));
            AddCandidate(Path.Combine(root, "Steam2"));
            AddCandidate(Path.Combine(root, "Steam_A"));
            AddCandidate(Path.Combine(root, "Steam_B"));

            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root, "Steam*", SearchOption.TopDirectoryOnly))
                {
                    AddCandidate(directory);
                }
            }
            catch
            {
                // Some system folders may not be accessible.
            }
        }

        AddCandidate(@"C:\Steam");
        AddCandidate(@"C:\Steam2");

        if (extraPaths is not null)
        {
            foreach (var path in extraPaths)
            {
                AddCandidate(path);
                AddCandidate(Path.GetDirectoryName(path));
            }
        }

        return found
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new SteamInstallationInfo(path))
            .ToList();
    }

    public IReadOnlyList<SteamAccountInfo> GetAccounts(string steamExe)
    {
        var directory = Path.GetDirectoryName(steamExe);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return [];
        }

        var loginUsers = Path.Combine(directory, "config", "loginusers.vdf");
        if (!File.Exists(loginUsers))
        {
            return [];
        }

        try
        {
            string? text = null;
            for (var attempt = 0; attempt < 3 && string.IsNullOrWhiteSpace(text); attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        loginUsers,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    text = reader.ReadToEnd();
                }
                catch (IOException) when (attempt < 2)
                {
                    Thread.Sleep(35);
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            var accounts = new List<SteamAccountInfo>();
            foreach (Match match in Regex.Matches(text, "(?ms)\"(?<id>\\d+)\"\\s*\\{(?<body>[^{}]*)\\}"))
            {
                var body = match.Groups["body"].Value;
                var account = Regex.Match(body, "\"AccountName\"\\s*\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(account))
                {
                    continue;
                }

                var persona = Regex.Match(body, "\"PersonaName\"\\s*\"([^\"]+)\"").Groups[1].Value;
                var remember = Regex.Match(body, "\"RememberPassword\"\\s*\"([^\"]+)\"").Groups[1].Value;
                var recent = Regex.Match(body, "\"MostRecent\"\\s*\"([^\"]+)\"").Groups[1].Value;
                accounts.Add(new SteamAccountInfo(account, persona, remember == "1", recent == "1"));
            }

            return accounts
                .OrderByDescending(account => account.MostRecent)
                .ThenByDescending(account => account.RememberPassword)
                .ThenBy(account => account.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
