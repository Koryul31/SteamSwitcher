using System.IO;
using System.Runtime.InteropServices;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed record ShortcutSpec(
    string Key,
    string Name,
    ShortcutMode Mode,
    string SteamExePath);

public sealed record ShortcutSyncResult(
    int Created,
    int Removed,
    IReadOnlyList<string> Paths,
    string? Error);

public sealed class ShortcutService
{
    public ShortcutSyncResult Sync(
        string backendPath,
        string workingDirectory,
        IReadOnlyList<ShortcutSpec> specs,
        ShortcutLayoutMode layout,
        bool createDesktop,
        bool createStartMenu)
    {
        if (!File.Exists(backendPath))
        {
            return new ShortcutSyncResult(0, 0, [], LocalizationService.T("StatusShortcutBackendMissing"));
        }

        var activeRoots = GetActiveRoots(layout, createDesktop, createStartMenu);
        var created = 0;
        var removed = 0;
        var paths = new List<string>();
        object? shell = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException(LocalizationService.T("StatusShortcutComUnavailable"));
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException(LocalizationService.T("StatusShortcutStartFailed"));
            dynamic wsh = shell;

            // Clean up managed shortcuts in every known location,
            // including locations used before the organization was changed.
            foreach (var root in GetManagedRoots())
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var link in Directory.EnumerateFiles(root, "*.lnk"))
                {
                    try
                    {
                        if (IsManagedShortcut(wsh, link))
                        {
                            File.Delete(link);
                            removed++;
                        }
                    }
                    catch
                    {
                        // A foreign or locked shortcut must not block the rest.
                    }
                }
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in specs)
            {
                var label = Sanitize(spec.Name);
                if (!names.Add(label))
                {
                    label = $"{label} ({spec.Key})";
                    names.Add(label);
                }

                foreach (var root in activeRoots)
                {
                    CreateForRoot(
                        wsh,
                        root,
                        label,
                        backendPath,
                        workingDirectory,
                        spec,
                        ref created,
                        paths);
                }
            }
        }
        catch (Exception ex)
        {
            return new ShortcutSyncResult(created, removed, paths, ex.Message);
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        return new ShortcutSyncResult(created, removed, paths, null);
    }

    public int CountManagedLinks(ShortcutLayoutMode layout, bool createDesktop, bool createStartMenu)
    {
        var roots = GetActiveRoots(layout, createDesktop, createStartMenu);
        if (roots.Count == 0)
        {
            return 0;
        }

        object? shell = null;
        var count = 0;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return 0;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return 0;
            }

            dynamic wsh = shell;
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var link in Directory.EnumerateFiles(root, "*.lnk"))
                {
                    try
                    {
                        if (IsManagedShortcut(wsh, link))
                        {
                            count++;
                        }
                    }
                    catch
                    {
                        // Foreign or locked shortcuts are not counted.
                    }
                }
            }
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        return count;
    }

    public string? CreateSetupShortcut(string setupExe, string label = "Configurar SteamSwitcher")
    {
        if (!File.Exists(setupExe))
        {
            return null;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{Sanitize(label)}.lnk");
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException(LocalizationService.T("StatusShortcutComUnavailable"));
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException(LocalizationService.T("StatusShortcutStartFailed"));
            dynamic wsh = shell;
            dynamic shortcut = wsh.CreateShortcut(path);
            try
            {
                shortcut.TargetPath = setupExe;
                shortcut.Arguments = string.Empty;
                shortcut.WorkingDirectory = Path.GetDirectoryName(setupExe) ?? string.Empty;
                shortcut.IconLocation = $"{setupExe},0";
                shortcut.Description = LocalizationService.T("ShortcutSetupDescription");
                shortcut.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            return path;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shell is not null)
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    public static string? GetActiveRoot(ShortcutLayoutMode layout, bool createDesktop, bool createStartMenu)
    {
        if (createDesktop)
        {
            return GetDesktopRoot(layout);
        }

        if (createStartMenu)
        {
            return GetStartMenuRoot(layout);
        }

        return null;
    }

    public static IReadOnlyList<string> GetActiveRoots(
        ShortcutLayoutMode layout,
        bool createDesktop,
        bool createStartMenu)
    {
        var roots = new List<string>();
        if (createDesktop)
        {
            roots.Add(GetDesktopRoot(layout));
        }

        if (createStartMenu)
        {
            roots.Add(GetStartMenuRoot(layout));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> GetManagedRoots() =>
    [
        GetDesktopRoot(ShortcutLayoutMode.Folder),
        GetDesktopRoot(ShortcutLayoutMode.Direct),
        GetStartMenuRoot(ShortcutLayoutMode.Folder),
        GetStartMenuRoot(ShortcutLayoutMode.Direct)
    ];

    private static string GetDesktopRoot(ShortcutLayoutMode layout) =>
        layout == ShortcutLayoutMode.Folder
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SteamSwitcher")
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private static string GetStartMenuRoot(ShortcutLayoutMode layout) =>
        layout == ShortcutLayoutMode.Folder
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "SteamSwitcher")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

    private static bool IsManagedShortcut(dynamic wsh, string path)
    {
        dynamic shortcut = wsh.CreateShortcut(path);
        try
        {
            string? target = shortcut.TargetPath as string;
            return !string.IsNullOrWhiteSpace(target) &&
                   Path.GetFileName(target).Equals("SteamSwitcher.exe", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
        }
    }

    private static string ResolveShortcutPath(dynamic wsh, string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        if (IsManagedShortcut(wsh, desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(desiredPath);
        var candidate = Path.Combine(directory, $"{fileName} (SteamSwitcher).lnk");
        var index = 2;
        while (File.Exists(candidate) && !IsManagedShortcut(wsh, candidate))
        {
            candidate = Path.Combine(directory, $"{fileName} (SteamSwitcher {index}).lnk");
            index++;
        }

        return candidate;
    }

    private static void CreateForRoot(
        dynamic wsh,
        string root,
        string label,
        string backendPath,
        string workingDirectory,
        ShortcutSpec spec,
        ref int created,
        ICollection<string> paths)
    {
        Directory.CreateDirectory(root);

        if (spec.Mode is ShortcutMode.Normal or ShortcutMode.Both)
        {
            CreateShortcut(
                wsh,
                ResolveShortcutPath(wsh, Path.Combine(root, $"{label}.lnk")),
                backendPath,
                workingDirectory,
                spec.Key,
                spec.SteamExePath,
                false,
                ref created,
                paths,
                spec.Name);
        }

        if (spec.Mode is ShortcutMode.BigPicture or ShortcutMode.Both)
        {
            CreateShortcut(
                wsh,
                ResolveShortcutPath(wsh, Path.Combine(root, $"{label} Big Picture.lnk")),
                backendPath,
                workingDirectory,
                spec.Key,
                spec.SteamExePath,
                true,
                ref created,
                paths,
                spec.Name);
        }
    }

    private static void CreateShortcut(
        dynamic wsh,
        string path,
        string backendPath,
        string workingDirectory,
        string key,
        string steamExePath,
        bool bigPicture,
        ref int created,
        ICollection<string> paths,
        string displayName)
    {
        dynamic shortcut = wsh.CreateShortcut(path);
        try
        {
            shortcut.TargetPath = backendPath;
            shortcut.Arguments = bigPicture
                ? $"--profile \"{key}\" --bigpicture"
                : $"--profile \"{key}\"";
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = $"{steamExePath},0";
            shortcut.Description = bigPicture
                ? LocalizationService.T("ShortcutBigDescription", displayName)
                : LocalizationService.T("ShortcutNormalDescription", displayName);
            shortcut.Save();
            paths.Add(path);
            created++;
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(name.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(value) ? "Steam" : value;
    }
}

