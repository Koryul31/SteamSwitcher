using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using SteamSwitcher.Core.Models;

namespace SteamSwitcher.Core.Services;

public interface IShortcutService
{
    string CreateShortcut(
        string shortcutPath,
        string targetExePath,
        string arguments,
        string workingDirectory,
        string description,
        string? iconPath = null,
        int iconIndex = 0);

    bool RemoveShortcut(string shortcutPath);
}

public sealed class ShortcutService : IShortcutService
{
    public string CreateShortcut(
        string shortcutPath,
        string targetExePath,
        string arguments,
        string workingDirectory,
        string description,
        string? iconPath = null,
        int iconIndex = 0)
    {
        var dir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetExePath);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(workingDirectory);
        link.SetDescription(description);

        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            link.SetIconLocation(iconPath, iconIndex);
        }
        else if (File.Exists(targetExePath))
        {
            link.SetIconLocation(targetExePath, 0);
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, true);

        return shortcutPath;
    }

    public bool RemoveShortcut(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
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
