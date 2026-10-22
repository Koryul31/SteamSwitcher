using System.IO;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed class BackendConfiguration
{
    public required string RootDirectory { get; init; }
    public required string SettingsPath { get; init; }
    public required string BackendPath { get; init; }
    public string BackupDirectory => Path.Combine(RootDirectory, "backup");
    public bool ConfirmSwitch { get; set; } = false;
    public int ShutdownTimeoutSeconds { get; set; } = 20;
    public List<BackendProfile> Profiles { get; } = [];

    public static BackendConfiguration Load(SetupContext context)
    {
        var configuration = new BackendConfiguration
        {
            RootDirectory = context.RootDirectory,
            SettingsPath = context.SettingsPath,
            BackendPath = context.BackendPath
        };

        var document = IniDocument.Load(context.SettingsPath);
        var general = document.Sections.TryGetValue("General", out var generalValues) ? generalValues : null;
        if (general is not null)
        {
            if (general.TryGetValue("ConfirmSwitch", out var confirm))
            {
                configuration.ConfirmSwitch = !confirm.Equals("false", StringComparison.OrdinalIgnoreCase);
            }

            if (general.TryGetValue("ShutdownTimeoutSeconds", out var timeout) &&
                int.TryParse(timeout, out var timeoutValue))
            {
                configuration.ShutdownTimeoutSeconds = Math.Clamp(timeoutValue, 5, 120);
            }
        }

        foreach (var section in document.Sections)
        {
            if (!section.Key.StartsWith("Profile:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = section.Key["Profile:".Length..].Trim();
            section.Value.TryGetValue("Name", out var name);
            section.Value.TryGetValue("ExePath", out var exePath);
            section.Value.TryGetValue("Login", out var login);
            configuration.Profiles.Add(new BackendProfile
            {
                Key = key,
                Name = name ?? key,
                ExePath = exePath ?? string.Empty,
                Login = login ?? string.Empty
            });
        }

        return configuration;
    }

    public void Save(IReadOnlyList<BackendProfile> profiles)
    {
        var document = IniDocument.Load(SettingsPath);
        document.Set("General", "ConfirmSwitch", ConfirmSwitch ? "true" : "false");
        document.Set("General", "ShutdownTimeoutSeconds", ShutdownTimeoutSeconds.ToString());
        document.RemoveSections(section => section.StartsWith("Profile:", StringComparison.OrdinalIgnoreCase));

        foreach (var profile in profiles)
        {
            var section = $"Profile:{profile.Key}";
            document.Set(section, "Name", profile.Name);
            document.Set(section, "ExePath", profile.ExePath);
            document.Set(section, "Login", profile.Login);
        }

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document.Save(SettingsPath);
    }

    public string CreateBackup()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var directory = Path.Combine(BackupDirectory, $"setup-{stamp}");
        Directory.CreateDirectory(directory);

        foreach (var path in new[] { SettingsPath, Path.Combine(RootDirectory, "setup.ini") })
        {
            if (File.Exists(path))
            {
                File.Copy(path, Path.Combine(directory, Path.GetFileName(path)), overwrite: true);
            }
        }

        return directory;
    }
}
