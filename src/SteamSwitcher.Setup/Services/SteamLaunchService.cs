using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SteamSwitcher.Setup.Localization;

namespace SteamSwitcher.Setup.Services;

public sealed record SteamLaunchResult(bool Success, string Message);
public sealed record SteamStopResult(bool Success, string Message);

public sealed class SteamLaunchService
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public async Task<SteamLaunchResult> RestartAsync(
        string targetExe,
        string? accountName,
        int shutdownTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(targetExe))
        {
            return new SteamLaunchResult(false, LocalizationService.T("StatusSteamNotFound"));
        }

        var target = Path.GetFullPath(targetExe);
        var running = GetRunningSteam();
        var targetRunning = running.FirstOrDefault(process =>
            !string.IsNullOrWhiteSpace(process.ExePath) &&
            string.Equals(Path.GetFullPath(process.ExePath), target, StringComparison.OrdinalIgnoreCase));

        if (targetRunning is not null)
        {
            // The requested copy is already running. Restore and focus it
            // without closing it or launching a second instance.
            if (TryFocusProcess(targetExe) || TryOpenSteamUri(targetExe))
            {
                return new SteamLaunchResult(
                    true,
                    LocalizationService.T("StatusSteamActivated"));
            }

            return new SteamLaunchResult(
                false,
                LocalizationService.T("StatusSteamActivationFailed"));
        }

        if (running.Count > 0)
        {
            var stopResult = await StopAllAsync(shutdownTimeoutSeconds, cancellationToken);
            if (!stopResult.Success)
            {
                return new SteamLaunchResult(false, stopResult.Message);
            }
        }

        var arguments = "-login";
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            arguments += $" \"{accountName}\"";
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(targetExe) ?? string.Empty,
                UseShellExecute = false
            });
            return new SteamLaunchResult(true, LocalizationService.T("StatusSteamOpenSuccess"));
        }
        catch (Exception ex)
        {
            return new SteamLaunchResult(false, LocalizationService.T("StatusSteamOpenFailed", ex.Message));
        }
    }

    public bool IsSteamRunning(string targetExe)
    {
        var target = Path.GetFullPath(targetExe);
        return GetRunningSteam().Any(process => PathsEqual(process.ExePath, target));
    }

    public bool IsOtherSteamRunning(string targetExe)
    {
        var target = Path.GetFullPath(targetExe);
        return GetRunningSteam().Any(process =>
            !string.IsNullOrWhiteSpace(process.ExePath) &&
            !PathsEqual(process.ExePath, target));
    }

    private static bool TryFocusProcess(string targetExe)
    {
        try
        {
            var handle = FindSteamWindow(targetExe);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            // SW_RESTORE recovers a minimized window; SW_SHOW recovers
            // a window hidden in the system tray.
            ShowWindowAsync(handle, SwRestore);
            ShowWindowAsync(handle, SwShow);
            BringWindowToTop(handle);
            SetForegroundWindow(handle);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr FindSteamWindow(string targetExe)
    {
        var installDirectory = Path.GetDirectoryName(Path.GetFullPath(targetExe));
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return IntPtr.Zero;
        }

        installDirectory = installDirectory.TrimEnd('\\') + "\\";
        IntPtr bestVisible = IntPtr.Zero;
        IntPtr hiddenFallback = IntPtr.Zero;
        long bestArea = 0;

        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerPid);
            if (ownerPid == 0)
            {
                return true;
            }

            var processPath = TryGetProcessPathById((int)ownerPid);
            if (!IsPathInside(processPath, installDirectory))
            {
                return true;
            }

            var width = 0;
            var height = 0;
            if (GetWindowRect(window, out var rect))
            {
                width = rect.Right - rect.Left;
                height = rect.Bottom - rect.Top;
            }

            if (IsWindowVisible(window) && width >= 320 && height >= 200)
            {
                var area = (long)width * height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestVisible = window;
                }

                return true;
            }

            if (hiddenFallback == IntPtr.Zero && LooksLikeSteamWindow(window))
            {
                hiddenFallback = window;
            }

            return true;
        }, IntPtr.Zero);

        return bestVisible != IntPtr.Zero ? bestVisible : hiddenFallback;
    }

    private static bool LooksLikeSteamWindow(IntPtr window)
    {
        var title = new StringBuilder(512);
        GetWindowText(window, title, title.Capacity);
        return title.ToString().Contains("Steam", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryOpenSteamUri(string targetExe)
    {
        if (!PathsEqual(GetSteamProtocolExePath(), targetExe))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://open/main",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetSteamProtocolExePath()
    {
        string? command = null;

        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Classes\steam\shell\open\command"))
            {
                command = key?.GetValue(null) as string;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                using var key = Registry.ClassesRoot.OpenSubKey(
                    @"steam\shell\open\command");
                command = key?.GetValue(null) as string;
            }
        }
        catch
        {
            return null;
        }

        return ExtractExecutablePath(command);
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        if (text.StartsWith('"'))
        {
            var closingQuote = text.IndexOf('"', 1);
            return closingQuote > 1 ? text[1..closingQuote].Trim() : null;
        }

        var separator = text.IndexOf(' ');
        return (separator > 0 ? text[..separator] : text).Trim();
    }

    private static bool PathsEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd('\\'),
                Path.GetFullPath(second).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                first.Trim().TrimEnd('\\'),
                second.Trim().TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<SteamStopResult> StopAllAsync(
        int shutdownTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var running = GetRunningSteam();
        if (running.Count == 0)
        {
            return new SteamStopResult(true, string.Empty);
        }

        foreach (var path in running
                     .Select(item => item.ExePath)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TryRequestShutdown(path!);
        }

        var timeout = Math.Clamp(shutdownTimeoutSeconds, 5, 120);
        if (!await WaitUntilStoppedAsync(TimeSpan.FromSeconds(timeout), cancellationToken))
        {
            foreach (var processId in GetRunningSteam().Select(item => item.ProcessId))
            {
                TryCloseWindow(processId);
            }

            if (!await WaitUntilStoppedAsync(TimeSpan.FromSeconds(3), cancellationToken))
            {
                foreach (var processId in GetRunningSteam().Select(item => item.ProcessId))
                {
                    TryKill(processId);
                }

                await WaitUntilStoppedAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        if (GetRunningSteam().Count > 0)
        {
            return new SteamStopResult(false, LocalizationService.T("StatusSteamCloseFailed"));
        }

        await Task.Delay(500, cancellationToken);
        return new SteamStopResult(true, string.Empty);
    }

    private static IReadOnlyList<RunningSteamProcess> GetRunningSteam()
    {
        var result = new List<RunningSteamProcess>();
        foreach (var process in Process.GetProcessesByName("steam"))
        {
            try
            {
                result.Add(new RunningSteamProcess(process.Id, TryGetProcessPath(process)));
            }
            catch
            {
                // The process may have exited between enumeration and lookup.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
            // MainModule can fail if Steam is elevated. Try the limited API.
        }

        var handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? buffer.ToString()
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
            }
        }
    }

    private static string? TryGetProcessPathById(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return TryGetProcessPath(process);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPathInside(string? candidate, string directory)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            return Path.GetFullPath(candidate).StartsWith(
                directory,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryRequestShutdown(string steamExePath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = steamExePath,
                Arguments = "-shutdown",
                WorkingDirectory = Path.GetDirectoryName(steamExePath) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit(5000);
        }
        catch
        {
            // If this fails, try closing the window and then the process.
        }
    }

    private static async Task<bool> WaitUntilStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GetRunningSteam().Count == 0)
            {
                return true;
            }

            await Task.Delay(250, cancellationToken);
        }

        return GetRunningSteam().Count == 0;
    }

    private static void TryCloseWindow(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.CloseMainWindow();
            }
        }
        catch
        {
            // The process has already exited.
        }
    }

    private static void TryKill(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process has already exited or does not allow closing.
        }
    }

    private sealed record RunningSteamProcess(int ProcessId, string? ExePath);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int SwShow = 5;
    private const int SwRestore = 9;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        uint flags,
        StringBuilder exeName,
        ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

