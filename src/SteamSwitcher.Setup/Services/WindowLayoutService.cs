using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SteamSwitcher.Setup.Services;

public static class WindowLayoutService
{
    private const uint MonitorDefaultToNearest = 2;
    

    public static void Fit(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                FitToWorkArea(window, SystemParameters.WorkArea);
                return;
            }

            var dpi = GetDpiForWindow(handle);
            var scale = dpi <= 0 ? 1.0 : dpi / 96.0;
            var work = new Rect(
                info.Work.Left / scale,
                info.Work.Top / scale,
                (info.Work.Right - info.Work.Left) / scale,
                (info.Work.Bottom - info.Work.Top) / scale);

            FitToWorkArea(window, work);
        }
        catch
        {
            FitToWorkArea(window, SystemParameters.WorkArea);
        }
    }

    public static void SetCornerPreference(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var preference = 2;
            DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int));
        }
        catch
        {
            // DWM may be unavailable in remote environments.
        }
    }

    private static void FitToWorkArea(Window window, Rect work)
    {
        var width = Math.Min(1000, Math.Max(480, work.Width - 32));
        var height = Math.Min(720, Math.Max(360, work.Height - 32));
        window.Width = width;
        window.Height = height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = work.Left + Math.Max(0, (work.Width - width) / 2);
        window.Top = work.Top + Math.Max(0, (work.Height - height) / 2);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct Monitor;
        public RectStruct Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

