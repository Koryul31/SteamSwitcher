using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed record ShortcutSpec(
    string Key,
    string Name,
    ShortcutMode Mode,
    string SteamExePath,
    string? DirectoryPath = null);

public sealed record ShortcutSyncResult(
    int Created,
    int Removed,
    IReadOnlyList<string> Paths,
    string? Error);

public sealed class ShortcutService
{
    private readonly Core.Services.ShortcutService _coreShortcut = new();

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

        try
        {
            // Clean up managed shortcuts in every known location
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
                        if (IsManagedShortcut(link))
                        {
                            File.Delete(link);
                            removed++;
                        }
                    }
                    catch
                    {
                        // A locked shortcut must not block the rest
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

        return new ShortcutSyncResult(created, removed, paths, null);
    }

    public int CountManagedLinks(ShortcutLayoutMode layout, bool createDesktop, bool createStartMenu)
    {
        var roots = GetActiveRoots(layout, createDesktop, createStartMenu);
        if (roots.Count == 0)
        {
            return 0;
        }

        var count = 0;
        try
        {
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
                        if (IsManagedShortcut(link))
                        {
                            count++;
                        }
                    }
                    catch { }
                }
            }
        }
        catch
        {
            return 0;
        }

        return count;
    }

    public string? CreateSetupShortcut(string setupExe, string? label = null)
    {
        if (!File.Exists(setupExe))
        {
            return null;
        }

        var title = !string.IsNullOrWhiteSpace(label)
            ? label
            : (LocalizationService.T("ShortcutSetupTitle") ?? "SteamSwitcher Setup");

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{Sanitize(title)}.lnk");

        try
        {
            _coreShortcut.CreateShortcut(
                path,
                setupExe,
                string.Empty,
                Path.GetDirectoryName(setupExe) ?? string.Empty,
                LocalizationService.T("ShortcutSetupDescription") ?? "Configure SteamSwitcher",
                setupExe,
                0);

            return path;
        }
        catch
        {
            return null;
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

    private static bool IsManagedShortcut(string path)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            var file = (IPersistFile)link;
            file.Load(path, 0);

            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, out _, 0);
            var target = sb.ToString();

            return !string.IsNullOrWhiteSpace(target) &&
                   Path.GetFileName(target).Equals("SteamSwitcher.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveShortcutPath(string desiredPath)
    {
        if (!File.Exists(desiredPath) || IsManagedShortcut(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(desiredPath);
        var candidate = Path.Combine(directory, $"{fileName} (SteamSwitcher).lnk");
        var index = 2;
        while (File.Exists(candidate) && !IsManagedShortcut(candidate))
        {
            candidate = Path.Combine(directory, $"{fileName} (SteamSwitcher {index}).lnk");
            index++;
        }

        return candidate;
    }

    private void CreateForRoot(
        string root,
        string label,
        string backendPath,
        string workingDirectory,
        ShortcutSpec spec,
        ref int created,
        ICollection<string> paths)
    {
        Directory.CreateDirectory(root);

        var argsBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(spec.DirectoryPath))
        {
            argsBuilder.Append($"--dir \"{spec.DirectoryPath}\" ");
        }
        if (!string.IsNullOrWhiteSpace(spec.Key))
        {
            argsBuilder.Append($"--account \"{spec.Key}\"");
        }
        var baseArgs = argsBuilder.ToString().Trim();

        if (spec.Mode is ShortcutMode.Normal or ShortcutMode.Both)
        {
            var lnkPath = ResolveShortcutPath(Path.Combine(root, $"{label}.lnk"));
            _coreShortcut.CreateShortcut(
                lnkPath,
                backendPath,
                baseArgs,
                workingDirectory,
                LocalizationService.T("ShortcutNormalDescription", spec.Name) ?? spec.Name,
                spec.SteamExePath,
                0);
            paths.Add(lnkPath);
            created++;
        }

        if (spec.Mode is ShortcutMode.BigPicture or ShortcutMode.Both)
        {
            var lnkPath = ResolveShortcutPath(Path.Combine(root, $"{label} Big Picture.lnk"));
            var bpArgs = string.IsNullOrWhiteSpace(baseArgs) ? "--bigpicture" : $"{baseArgs} --bigpicture";
            _coreShortcut.CreateShortcut(
                lnkPath,
                backendPath,
                bpArgs,
                workingDirectory,
                LocalizationService.T("ShortcutBigDescription", spec.Name) ?? spec.Name,
                spec.SteamExePath,
                0);
            paths.Add(lnkPath);
            created++;
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(value) ? "Steam" : value;
    }

    #region COM Interop

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    #endregion
}
