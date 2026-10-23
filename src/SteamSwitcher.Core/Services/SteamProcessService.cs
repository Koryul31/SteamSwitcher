using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamSwitcher.Core.Services;

public interface ISteamProcessService
{
    bool IsSteamRunning();
    IReadOnlyList<int> GetRunningSteamProcessIds();
    Task<bool> ShutdownSteamAsync(string steamExePath, TimeSpan timeout, CancellationToken cancellationToken = default);
    bool LaunchSteam(string steamExePath, string arguments = "");
    bool ActivateSteamWindow(string steamExePath);
}

public sealed class SteamProcessService : ISteamProcessService
{
    public bool IsSteamRunning()
    {
        return Process.GetProcessesByName("steam").Length > 0;
    }

    public IReadOnlyList<int> GetRunningSteamProcessIds()
    {
        return Process.GetProcessesByName("steam").Select(p => p.Id).ToList();
    }

    public async Task<bool> ShutdownSteamAsync(string steamExePath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsSteamRunning())
        {
            return true;
        }

        // 1. Send graceful -shutdown command
        try
        {
            if (File.Exists(steamExePath))
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = steamExePath,
                    Arguments = "-shutdown",
                    WorkingDirectory = Path.GetDirectoryName(steamExePath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(5000);
            }
        }
        catch { }

        // 2. Poll until all steam.exe processes exit (allows cloud save sync to complete)
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSteamRunning())
            {
                // Small buffer to let file locks release
                await Task.Delay(500, cancellationToken);
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return !IsSteamRunning();
    }

    public bool LaunchSteam(string steamExePath, string arguments = "")
    {
        if (!File.Exists(steamExePath))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = steamExePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(steamExePath) ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ActivateSteamWindow(string steamExePath)
    {
        var installDir = Path.GetDirectoryName(Path.GetFullPath(steamExePath))?.TrimEnd('\\') + "\\";
        if (string.IsNullOrWhiteSpace(installDir)) return false;

        IntPtr bestWindow = IntPtr.Zero;
        long bestArea = 0;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0) return true;

            var path = TryGetProcessPath((int)pid);
            if (path == null || !path.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsWindowVisible(hWnd) && GetWindowRect(hWnd, out var rect))
            {
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width >= 320 && height >= 200)
                {
                    var area = (long)width * height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestWindow = hWnd;
                    }
                }
            }

            return true;
        }, IntPtr.Zero);

        if (bestWindow != IntPtr.Zero)
        {
            ShowWindowAsync(bestWindow, 9 /* SW_RESTORE */);
            ShowWindowAsync(bestWindow, 5 /* SW_SHOW */);
            BringWindowToTop(bestWindow);
            SetForegroundWindow(bestWindow);
            return true;
        }

        return false;
    }

    private static string? TryGetProcessPath(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            return p.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    #endregion
}
