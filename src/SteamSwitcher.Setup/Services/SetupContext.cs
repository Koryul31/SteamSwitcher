using System.IO;

namespace SteamSwitcher.Setup.Services;

public sealed class SetupContext
{
    private SetupContext(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }
    public string SettingsPath => Path.Combine(RootDirectory, "settings.ini");
    public string SetupPath => Path.Combine(RootDirectory, "setup.ini");
    public string BackendPath => Path.Combine(RootDirectory, "SteamSwitcher.exe");
    public string BackupDirectory => Path.Combine(RootDirectory, "backup");
    public bool HasSettings => File.Exists(SettingsPath);
    public bool HasBackend => File.Exists(BackendPath);

    public static SetupContext Locate()
    {
        var environmentRoot = Environment.GetEnvironmentVariable("STEAMSWITCHER_HOME");
        if (!string.IsNullOrWhiteSpace(environmentRoot) && LooksLikeRoot(environmentRoot))
        {
            return new SetupContext(Path.GetFullPath(environmentRoot));
        }

        var candidates = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, "SteamSwitcher"));
        }

        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var directory = baseDirectory; directory is not null; directory = directory.Parent)
        {
            candidates.Add(directory.FullName);
            candidates.Add(Path.Combine(directory.FullName, "SteamSwitcher"));
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "app"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (LooksLikeRoot(candidate))
            {
                return new SetupContext(candidate);
            }
        }

        var fallback = !string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(localAppData, "SteamSwitcher")
            : AppContext.BaseDirectory;

        return new SetupContext(fallback);
    }

    private static bool LooksLikeRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "settings.ini")) ||
               File.Exists(Path.Combine(path, "SteamSwitcher.exe"));
    }
}
