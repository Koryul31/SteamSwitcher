using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("SteamSwitcher")]
[assembly: System.Reflection.AssemblyDescription("Switch between two independent Steam installations")]
[assembly: System.Reflection.AssemblyProduct("SteamSwitcher")]
[assembly: System.Reflection.AssemblyCompany("Koryul")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 Koryul")]
[assembly: System.Reflection.AssemblyTrademark("")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.3.2")]
[assembly: System.Reflection.AssemblyVersion("1.3.2.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.2.0")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: System.Runtime.InteropServices.Guid("6D0B7A5E-4D5B-4A7B-9C16-3C9A8D6F2A10")]

namespace SteamSwitcher
{
    internal static class Program
    {
        private const string AppName = "Steam Switcher";
        private const string MutexName = @"Local\SteamSwitcher-6D0B7A5E";
        private const long MaxLogBytes = 512 * 1024;
        private const uint ProcessQueryLimitedInformation = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr processHandle,
            uint flags,
            StringBuilder exeName,
            ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string LogDir = Path.Combine(BaseDir, "logs");
        private static readonly string LogPath = Path.Combine(LogDir, "switcher.log");

        private static readonly HashSet<string> ClientProcessNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "steam",
                "steamwebhelper",
                "gameoverlayui",
                "steamerrorreporter",
                "steamtours"
            };

        private static bool _noUi;

        [STAThread]
        private static int Main(string[] args)
        {
            string profileKey = null;
            bool dryRun = false;
            bool bigPicture = false;
            _noUi = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;

                if (arg.Equals("--profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    profileKey = args[++i];
                }
                else if (arg.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase))
                {
                    profileKey = arg.Substring("--profile=".Length);
                }
                else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
                {
                    dryRun = true;
                }
                else if (arg.Equals("--no-ui", StringComparison.OrdinalIgnoreCase))
                {
                    _noUi = true;
                }
                else if (arg.Equals("--bigpicture", StringComparison.OrdinalIgnoreCase))
                {
                    bigPicture = true;
                }
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfo("Uso:\nSteamSwitcher.exe --profile <perfil> [--bigpicture]\n\n" +
                             "Los perfiles se definen en settings.ini.");
                    return 0;
                }
            }

            if (string.IsNullOrWhiteSpace(profileKey))
            {
                ShowError("Falta indicar qué perfil abrir.\n\nUso: SteamSwitcher.exe --profile <perfil>");
                return 2;
            }

            RotateLogIfNeeded();

            bool mutexAcquired = false;
            using (Mutex mutex = new Mutex(false, MutexName))
            {
                try
                {
                    try
                    {
                        mutexAcquired = mutex.WaitOne(TimeSpan.FromSeconds(45));
                    }
                    catch (AbandonedMutexException)
                    {
                        mutexAcquired = true;
                    }

                    if (!mutexAcquired)
                    {
                        Log("No se pudo adquirir el mutex: ya hay otro cambio en curso.");
                        ShowInfo("Ya hay otro cambio de Steam en curso. Espera unos segundos y vuelve a intentarlo.");
                        return 3;
                    }

                    return Run(profileKey.Trim(), dryRun, bigPicture);
                }
                catch (Exception ex)
                {
                    Log("ERROR no controlado: " + ex);
                    ShowError("Ocurrió un error inesperado.\n\n" + ex.Message + "\n\nRevisa el log:\n" + LogPath);
                    return 1;
                }
                finally
                {
                    if (mutexAcquired)
                    {
                        try { mutex.ReleaseMutex(); }
                        catch { }
                    }
                }
            }
        }

        private static int Run(string profileKey, bool dryRun, bool bigPicture)
        {
            string settingsPath = Path.Combine(BaseDir, "settings.ini");
            Settings settings = Settings.Load(settingsPath);

            SteamProfile target = settings.GetProfile(profileKey);
            if (target == null)
            {
                string message = "El perfil '" + profileKey + "' no existe en settings.ini.";
                Log("ERROR: " + message);
                ShowError(message);
                return 1;
            }

            if (!File.Exists(target.ExePath))
            {
                string message = "No se encontró Steam en:\n" + target.ExePath + "\n\nRevisa settings.ini.";
                Log("ERROR: " + message.Replace("\n", " "));
                ShowError(message);
                return 1;
            }

            Log("Inicio. Perfil solicitado: " + target.Key + " (" + target.Name + ")" +
                (bigPicture ? " [Big Picture]" : string.Empty) +
                (dryRun ? " [dry-run]" : string.Empty));

            List<RunningSteam> running = RunningSteam.FindAll(settings);
            LogRunning(running);

            RunningSteam targetRunning = running.Find(
                delegate(RunningSteam item)
                {
                    return item.Profile.Key.Equals(target.Key, StringComparison.OrdinalIgnoreCase);
                });

            if (targetRunning != null)
            {
                if (bigPicture)
                {
                    Log(target.Name + " ya está abierto (PID " + targetRunning.ProcessId + "). Se abrirá Big Picture.");

                    if (dryRun)
                    {
                        Log("[dry-run] Se abriría Big Picture en " + target.Name + ".");
                        return 0;
                    }

                    if (!OpenBigPicture(target))
                    {
                        ShowError("No se pudo abrir Big Picture en " + target.Name +
                                  ".\n\nRevisa el log:\n" + LogPath);
                        return 7;
                    }

                    FocusBigPicture(target);
                    return 0;
                }

                Log(target.Name + " ya está abierto (PID " + targetRunning.ProcessId +
                    "). Se intentará restaurar y enfocar su ventana.");

                if (dryRun)
                {
                    Log("[dry-run] Se restauraría y enfocaría " + target.Name + ".");
                    return 0;
                }

                IntPtr existingWindow = FindSteamWindow(target);
                if (existingWindow != IntPtr.Zero && TryActivateWindow(existingWindow))
                {
                    Log("Ventana de " + target.Name + " restaurada y enfocada.");
                    return 0;
                }

                if (TryOpenSteamMainWindow(target))
                {
                    Log("Se solicitó a Steam que abriera su ventana principal.");
                    return 0;
                }

                // Never block the user with an "already running" warning.
                // If Steam is in the tray and exposes no window, let the client
                // keep running without launching another copy.
                Log("Aviso: " + target.Name + " está en ejecución, pero no se encontró una ventana para enfocar.");
                return 0;
            }

            List<RunningSteam> others = running.FindAll(
                delegate(RunningSteam item)
                {
                    return !item.Profile.Key.Equals(target.Key, StringComparison.OrdinalIgnoreCase);
                });

            if (others.Count == 0)
            {
                Log("No hay otra instalación de Steam abierta.");
            }
            else
            {
                string otherNames = JoinProfileNames(others);
                Log("Instalación abierta que hay que cerrar: " + otherNames);

                if (dryRun)
                {
                    Log("[dry-run] Se cerraría " + otherNames + " y se abriría " + target.Name + ".");
                    return 0;
                }

                if (settings.ConfirmSwitch)
                {
                    string question = otherNames + " está abierto.\n\n¿Cerrarlo e iniciar " + target.Name +
                                      "?\n\nSi hay un juego abierto en esa instalación, se cerrará.";
                    if (!Confirm(question))
                    {
                        Log("El usuario canceló el cambio.");
                        return 0;
                    }
                }

                if (!StopOtherProfiles(others, settings.ShutdownTimeoutSeconds))
                {
                    string message = "No se pudo cerrar " + otherNames +
                                     ".\n\nCiérralo manualmente y vuelve a intentarlo.\n\nRevisa el log:\n" + LogPath;
                    Log("ERROR: no se pudo cerrar " + otherNames + ".");
                    ShowError(message);
                    return 5;
                }

                Thread.Sleep(1500);
            }

            if (dryRun)
            {
                Log("[dry-run] Se abriría " + target.Name + " con: \"" + target.ExePath + "\" " + target.LaunchArguments +
                    (bigPicture ? " y después Big Picture." : string.Empty));
                return 0;
            }

            if (!LaunchTarget(target, settings.StartupTimeoutSeconds))
            {
                string message = "No se pudo iniciar " + target.Name +
                                 ".\n\nPuede que Steam haya tardado demasiado o se haya cerrado al instante." +
                                 "\n\nRevisa el log:\n" + LogPath;
                ShowError(message);
                return 6;
            }

            if (bigPicture && !OpenBigPicture(target))
            {
                ShowError("Steam se inició, pero no se pudo abrir Big Picture en " + target.Name +
                          ".\n\nRevisa el log:\n" + LogPath);
                return 7;
            }

            if (bigPicture)
            {
                FocusBigPicture(target);
            }

            return 0;
        }

        private static bool OpenBigPicture(SteamProfile profile)
        {
            Log("Esperando a que el protocolo steam:// apunte a " + profile.Name + " para abrir Big Picture...");

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            string handlerPath = null;

            while (DateTime.UtcNow < deadline)
            {
                handlerPath = GetSteamProtocolExePath();
                if (!string.IsNullOrWhiteSpace(handlerPath) && PathsEqual(handlerPath, profile.ExePath))
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (string.IsNullOrWhiteSpace(handlerPath) || !PathsEqual(handlerPath, profile.ExePath))
            {
                Log("ERROR: el protocolo steam:// apunta a " +
                    (string.IsNullOrWhiteSpace(handlerPath) ? "(desconocido)" : handlerPath) +
                    ", no a " + profile.ExePath + ". No se abrirá Big Picture.");
                return false;
            }

            Log("Abriendo Big Picture en " + profile.Name + " con steam://open/bigpicture...");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "steam://open/bigpicture";
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                Log("Big Picture solicitado para " + profile.Name + ".");
                return true;
            }
            catch (Exception ex)
            {
                Log("ERROR al abrir Big Picture en " + profile.Name + ": " + ex);
                return false;
            }
        }

        private static void FocusBigPicture(SteamProfile profile)
        {
            Thread.Sleep(1500);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            bool loggedCandidate = false;

            while (DateTime.UtcNow < deadline)
            {
                IntPtr hwnd = FindSteamWindow(profile);
                if (hwnd != IntPtr.Zero)
                {
                    if (!loggedCandidate)
                    {
                        Log("Ventana de Steam detectada; intentando traer Big Picture al primer plano.");
                        loggedCandidate = true;
                    }

                    if (GetForegroundWindow() == hwnd)
                    {
                        Log("Big Picture ya está en primer plano.");
                        return;
                    }

                    if (TryActivateWindow(hwnd))
                    {
                        Log("Big Picture traído al primer plano.");
                        return;
                    }
                }

                Thread.Sleep(500);
            }

            Log("Aviso: no se pudo confirmar el primer plano de Big Picture.");
        }

        private static IntPtr FindSteamWindow(SteamProfile profile)
        {
            string installDir = EnsureTrailingSeparator(profile.InstallDirectory);
            IntPtr bestVisible = IntPtr.Zero;
            IntPtr hiddenFallback = IntPtr.Zero;
            long bestArea = 0;

            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                if (processId == 0)
                {
                    return true;
                }

                string processPath = GetProcessPathById(processId);
                if (processPath == null || !IsPathInside(processPath, installDir))
                {
                    return true;
                }

                RECT rect;
                long width = 0;
                long height = 0;
                if (GetWindowRect(hWnd, out rect))
                {
                    width = rect.Right - rect.Left;
                    height = rect.Bottom - rect.Top;
                }

                bool visible = IsWindowVisible(hWnd);
                if (visible && width >= 320 && height >= 200)
                {
                    long area = width * height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestVisible = hWnd;
                    }

                    return true;
                }

                if (hiddenFallback == IntPtr.Zero && LooksLikeSteamWindow(hWnd))
                {
                    hiddenFallback = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return bestVisible != IntPtr.Zero ? bestVisible : hiddenFallback;
        }

        private static bool LooksLikeSteamWindow(IntPtr hWnd)
        {
            StringBuilder title = new StringBuilder(512);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString().IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryOpenSteamMainWindow(SteamProfile target)
        {
            string handlerPath = GetSteamProtocolExePath();
            if (!PathsEqual(handlerPath, target.ExePath))
            {
                Log("El protocolo steam:// no apunta a " + target.Name + "; no se usa para restaurar la ventana.");
                return false;
            }

            try
            {
                Log("Solicitando steam://open/main para " + target.Name + "...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "steam://open/main",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Log("Aviso: no se pudo abrir la ventana principal de " + target.Name + ": " + ex.Message);
                return false;
            }
        }

        private static string GetProcessPathById(uint processId)
        {
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return TryGetProcessPath(process);
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool TryActivateWindow(IntPtr hWnd)
        {
            ShowWindow(hWnd, IsIconic(hWnd) ? 9 : 5);
            BringWindowToTop(hWnd);

            if (SetForegroundWindow(hWnd))
            {
                return true;
            }

            IntPtr foreground = GetForegroundWindow();
            uint foregroundProcessId;
            uint foregroundThread = GetWindowThreadProcessId(foreground, out foregroundProcessId);
            uint currentThread = GetCurrentThreadId();

            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                bool attached = AttachThreadInput(currentThread, foregroundThread, true);
                try
                {
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                }
                finally
                {
                    if (attached)
                    {
                        AttachThreadInput(currentThread, foregroundThread, false);
                    }
                }
            }

            return GetForegroundWindow() == hWnd;
        }

        private static bool LaunchTarget(SteamProfile target, int timeoutSeconds)
        {
            const int attempts = 2;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                if (IsProfileProcessRunning(target))
                {
                    Log(target.Name + " ya está activo antes del intento " + attempt + ".");
                    return true;
                }

                Log("Abriendo " + target.Name + " con: \"" + target.ExePath + "\" " +
                    target.LaunchArguments + " (intento " + attempt + " de " + attempts + ")");

                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = target.ExePath;
                    startInfo.Arguments = target.LaunchArguments;
                    startInfo.WorkingDirectory = target.InstallDirectory;
                    startInfo.UseShellExecute = true;
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Log("ERROR al iniciar " + target.Name + " (intento " + attempt + "): " + ex);
                    if (attempt < attempts)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }

                    return false;
                }

                if (WaitForProfileProcess(target, timeoutSeconds))
                {
                    Log(target.Name + " iniciado correctamente.");
                    return true;
                }

                Log("Aviso: " + target.Name + " no quedó abierto en el intento " + attempt + ".");

                if (attempt < attempts)
                {
                    Thread.Sleep(3000);
                }
            }

            Log("ERROR: " + target.Name + " no se pudo iniciar después de " + attempts + " intentos.");
            return false;
        }

        private static bool StopOtherProfiles(List<RunningSteam> others, int timeoutSeconds)
        {
            bool allStopped = true;
            List<string> processed = new List<string>();

            foreach (RunningSteam other in others)
            {
                if (ContainsIgnoreCase(processed, other.Profile.Key))
                {
                    continue;
                }

                processed.Add(other.Profile.Key);

                List<RunningSteam> group = others.FindAll(
                    delegate(RunningSteam item)
                    {
                        return item.Profile.Key.Equals(other.Profile.Key, StringComparison.OrdinalIgnoreCase);
                    });

                if (!StopProfile(other.Profile, group, timeoutSeconds))
                {
                    allStopped = false;
                }
            }

            return allStopped;
        }

        private static bool StopProfile(SteamProfile profile, List<RunningSteam> instances, int timeoutSeconds)
        {
            List<int> pids = new List<int>();
            foreach (RunningSteam instance in instances)
            {
                pids.Add(instance.ProcessId);
            }

            Log("Solicitando cierre nativo de " + profile.Name + " (PIDs: " + JoinInts(pids) + ")...");

            int urlTimeout = Math.Min(10, timeoutSeconds);
            if (TryCloseWithSteamExitUrl(profile, pids, urlTimeout))
            {
                Log(profile.Name + " se cerró con steam://exit.");
                WaitForClientProcessesToExit(profile, 5);
                return true;
            }

            Log("steam://exit no cerró " + profile.Name + " a tiempo. Solicitando cierre con -shutdown...");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = profile.ExePath;
                startInfo.Arguments = "-shutdown";
                startInfo.WorkingDirectory = profile.InstallDirectory;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process shutdown = Process.Start(startInfo))
                {
                    if (shutdown != null)
                    {
                        shutdown.WaitForExit(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Aviso: falló la solicitud de cierre con -shutdown de " + profile.Name + ": " + ex.Message);
            }

            if (WaitForProcessesToExit(pids, profile.ExePath, timeoutSeconds))
            {
                Log(profile.Name + " se cerró con -shutdown.");
                WaitForClientProcessesToExit(profile, 5);
                return true;
            }

            Log("El cierre nativo no terminó a tiempo. Intentando cerrar la ventana principal...");

            foreach (int pid in pids)
            {
                try
                {
                    using (Process process = Process.GetProcessById(pid))
                    {
                        if (!process.HasExited)
                        {
                            process.CloseMainWindow();
                        }
                    }
                }
                catch
                {
                }
            }

            if (WaitForProcessesToExit(pids, profile.ExePath, 3))
            {
                Log(profile.Name + " se cerró después de solicitar el cierre de la ventana.");
                WaitForClientProcessesToExit(profile, 5);
                return true;
            }

            Log("Forzando el cierre de los procesos del cliente " + profile.Name + "...");
            ForceCloseProfile(profile);

            bool stopped = WaitForProcessesToExit(pids, profile.ExePath, 10);
            if (stopped)
            {
                Log(profile.Name + " se cerró forzadamente.");
                WaitForClientProcessesToExit(profile, 5);
            }
            else
            {
                Log("ERROR: " + profile.Name + " sigue abierto después de forzar el cierre.");
            }

            return stopped;
        }

        private static bool TryCloseWithSteamExitUrl(SteamProfile profile, List<int> pids, int timeoutSeconds)
        {
            string handlerPath = GetSteamProtocolExePath();

            if (string.IsNullOrWhiteSpace(handlerPath))
            {
                Log("No se pudo leer el manejador del protocolo steam://. Se omite steam://exit.");
                return false;
            }

            if (!PathsEqual(handlerPath, profile.ExePath))
            {
                Log("El protocolo steam:// apunta a " + handlerPath + ", no a " + profile.ExePath +
                    ". Se omite steam://exit para no abrir la instalación equivocada.");
                return false;
            }

            Log("El protocolo steam:// apunta a " + profile.Name + ". Enviando steam://exit...");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "steam://exit";
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Log("Aviso: falló steam://exit: " + ex.Message);
                return false;
            }

            return WaitForProcessesToExit(pids, profile.ExePath, timeoutSeconds);
        }

        private static string GetSteamProtocolExePath()
        {
            string command = null;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\steam\shell\open\command"))
                {
                    if (key != null)
                    {
                        command = key.GetValue(null) as string;
                    }
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"steam\shell\open\command"))
                    {
                        if (key != null)
                        {
                            command = key.GetValue(null) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Aviso: no se pudo leer el registro del protocolo steam://: " + ex.Message);
                return null;
            }

            return ExtractExecutablePath(command);
        }

        private static string ExtractExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            string trimmed = command.Trim();

            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                if (closingQuote <= 1)
                {
                    return null;
                }

                return Environment.ExpandEnvironmentVariables(trimmed.Substring(1, closingQuote - 1));
            }

            int firstSpace = trimmed.IndexOf(' ');
            string path = firstSpace >= 0 ? trimmed.Substring(0, firstSpace) : trimmed;
            return Environment.ExpandEnvironmentVariables(path);
        }

        private static bool WaitForClientProcessesToExit(SteamProfile profile, int timeoutSeconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                if (!AnyClientProcessRunning(profile))
                {
                    return true;
                }

                Thread.Sleep(250);
            }

            if (!AnyClientProcessRunning(profile))
            {
                return true;
            }

            Log("Quedaron procesos auxiliares de " + profile.Name + ". Cerrando el cliente por completo...");
            ForceCloseProfile(profile);

            DateTime secondDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < secondDeadline)
            {
                if (!AnyClientProcessRunning(profile))
                {
                    return true;
                }

                Thread.Sleep(250);
            }

            Log("Aviso: algunos procesos auxiliares de " + profile.Name + " siguen activos.");
            return false;
        }

        private static bool AnyClientProcessRunning(SteamProfile profile)
        {
            string installDir = EnsureTrailingSeparator(profile.InstallDirectory);
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                using (process)
                {
                    if (!ClientProcessNames.Contains(process.ProcessName))
                    {
                        continue;
                    }

                    string path = TryGetProcessPath(process);
                    if (path != null && IsPathInside(path, installDir))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ForceCloseProfile(SteamProfile profile)
        {
            string installDir = EnsureTrailingSeparator(profile.InstallDirectory);
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                using (process)
                {
                    if (!ClientProcessNames.Contains(process.ProcessName))
                    {
                        continue;
                    }

                    string path = TryGetProcessPath(process);
                    if (path == null || !IsPathInside(path, installDir))
                    {
                        continue;
                    }

                    try
                    {
                        if (!process.HasExited)
                        {
                            int pid = process.Id;
                            string name = process.ProcessName;
                            process.Kill();
                            process.WaitForExit(3000);
                            Log("Proceso cerrado: " + name + ".exe (PID " + pid + ")");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Aviso: no se pudo cerrar " + process.ProcessName + " (PID " + process.Id + "): " + ex.Message);
                    }
                }
            }
        }

        private static bool WaitForProcessesToExit(List<int> pids, string expectedExePath, int timeoutSeconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                if (!AnyProcessStillRunning(pids, expectedExePath))
                {
                    return true;
                }

                Thread.Sleep(250);
            }

            return !AnyProcessStillRunning(pids, expectedExePath);
        }

        private static bool AnyProcessStillRunning(List<int> pids, string expectedExePath)
        {
            foreach (int pid in pids)
            {
                if (IsSameProcessStillRunning(pid, expectedExePath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameProcessStillRunning(int pid, string expectedExePath)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    if (process.HasExited)
                    {
                        return false;
                    }

                    string path = TryGetProcessPath(process);
                    if (path == null)
                    {
                        return true;
                    }

                    return PathsEqual(path, expectedExePath);
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool WaitForProfileProcess(SteamProfile profile, int timeoutSeconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            bool seen = false;

            while (DateTime.UtcNow < deadline)
            {
                if (IsProfileProcessRunning(profile))
                {
                    if (!seen)
                    {
                        seen = true;
                        Log("El proceso de " + profile.Name + " apareció. Comprobando que se mantenga abierto...");
                        Thread.Sleep(2500);

                        if (IsProfileProcessRunning(profile))
                        {
                            return true;
                        }

                        Log("Aviso: el proceso de " + profile.Name + " apareció pero se cerró al instante.");
                        return false;
                    }
                }

                Thread.Sleep(300);
            }

            return seen && IsProfileProcessRunning(profile);
        }

        private static bool IsProfileProcessRunning(SteamProfile profile)
        {
            Process[] processes = Process.GetProcessesByName("steam");

            foreach (Process process in processes)
            {
                using (process)
                {
                    string path = TryGetProcessPath(process);
                    if (path != null && PathsEqual(path, profile.ExePath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static string TryGetProcessPath(Process process)
        {
            try
            {
                string mainModulePath = process.MainModule.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath))
                {
                    return mainModulePath;
                }
            }
            catch
            {
            }

            // MainModule can fail when Steam runs with higher privileges than
            // Steam Switcher. QueryFullProcessImageName with limited access
            // retrieves the path without elevating the switcher.
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                StringBuilder buffer = new StringBuilder(32768);
                int size = buffer.Capacity;
                if (!QueryFullProcessImageName(handle, 0, buffer, ref size) || size <= 0)
                {
                    return null;
                }

                return buffer.ToString(0, size);
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

        internal static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                string normalizedLeft = Path.GetFullPath(left).TrimEnd('\\');
                string normalizedRight = Path.GetFullPath(right).TrimEnd('\\');
                return normalizedLeft.Equals(normalizedRight, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return left.Trim().TrimEnd('\\').Equals(
                    right.Trim().TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\";
        }

        private static bool IsPathInside(string path, string directoryWithSeparator)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryWithSeparator))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                return fullPath.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsIgnoreCase(List<string> values, string candidate)
        {
            foreach (string value in values)
            {
                if (value.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string JoinProfileNames(List<RunningSteam> running)
        {
            List<string> names = new List<string>();

            foreach (RunningSteam item in running)
            {
                if (!ContainsIgnoreCase(names, item.Profile.Name))
                {
                    names.Add(item.Profile.Name);
                }
            }

            return string.Join(", ", names.ToArray());
        }

        private static string JoinInts(List<int> values)
        {
            List<string> parts = new List<string>();
            foreach (int value in values)
            {
                parts.Add(value.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(", ", parts.ToArray());
        }

        private static void LogRunning(List<RunningSteam> running)
        {
            if (running.Count == 0)
            {
                Log("No se detectó ninguna instalación de Steam en ejecución.");
                return;
            }

            foreach (RunningSteam item in running)
            {
                Log("Detectado: " + item.Profile.Name + " | PID " + item.ProcessId + " | " + item.ProcessPath);
            }
        }

        private static void RotateLogIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath))
                {
                    return;
                }

                FileInfo info = new FileInfo(LogPath);
                if (info.Length < MaxLogBytes)
                {
                    return;
                }

                string oldLog = LogPath + ".old";
                if (File.Exists(oldLog))
                {
                    File.Delete(oldLog);
                }

                File.Move(LogPath, oldLog);
            }
            catch
            {
            }
        }

        internal static void Log(string message)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }

                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                              "  " + message + Environment.NewLine;
                File.AppendAllText(LogPath, line, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static void ShowInfo(string message)
        {
            if (_noUi)
            {
                Log("Aviso (UI omitida): " + message.Replace(Environment.NewLine, " ").Replace("\n", " "));
                return;
            }

            MessageBox.Show(message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowError(string message)
        {
            if (_noUi)
            {
                Log("Error (UI omitida): " + message.Replace(Environment.NewLine, " ").Replace("\n", " "));
                return;
            }

            MessageBox.Show(message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static bool Confirm(string message)
        {
            if (_noUi)
            {
                Log("Confirmación omitida por --no-ui.");
                return true;
            }

            DialogResult result = MessageBox.Show(
                message,
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            return result == DialogResult.Yes;
        }
    }

    internal sealed class SteamProfile
    {
        public string Key;
        public string Name;
        public string ExePath;
        public string Login;
        public string InstallDirectory;

        public SteamProfile(string key, string name, string exePath, string login)
        {
            Key = key;
            Name = name;
            ExePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(exePath));
            Login = login;
            InstallDirectory = Path.GetDirectoryName(ExePath);
        }

        public string LaunchArguments
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Login))
                {
                    return string.Empty;
                }

                string account = Login.Trim();
                if (account.IndexOf(' ') >= 0)
                {
                    account = "\"" + account + "\"";
                }

                return "-login " + account;
            }
        }
    }

    internal sealed class RunningSteam
    {
        public SteamProfile Profile;
        public int ProcessId;
        public string ProcessPath;

        public static List<RunningSteam> FindAll(Settings settings)
        {
            List<RunningSteam> result = new List<RunningSteam>();
            Process[] processes = Process.GetProcessesByName("steam");

            foreach (Process process in processes)
            {
                using (process)
                {
                    string path = Program.TryGetProcessPath(process);
                    if (path == null)
                    {
                        Program.Log("Aviso: no se pudo leer la ruta de steam.exe (PID " + process.Id + ").");
                        continue;
                    }

                    SteamProfile profile = settings.FindProfileByExePath(path);
                    if (profile == null)
                    {
                        Program.Log("Aviso: steam.exe (PID " + process.Id + ") no coincide con ningún perfil: " + path);
                        continue;
                    }

                    RunningSteam item = new RunningSteam();
                    item.Profile = profile;
                    item.ProcessId = process.Id;
                    item.ProcessPath = path;
                    result.Add(item);
                }
            }

            return result;
        }
    }

    internal sealed class Settings
    {
        public bool ConfirmSwitch = false;
        public int ShutdownTimeoutSeconds = 25;
        public int StartupTimeoutSeconds = 25;

        private readonly Dictionary<string, SteamProfile> _profiles =
            new Dictionary<string, SteamProfile>(StringComparer.OrdinalIgnoreCase);

        public SteamProfile GetProfile(string key)
        {
            SteamProfile profile;
            if (_profiles.TryGetValue(key, out profile))
            {
                return profile;
            }

            return null;
        }

        public SteamProfile FindProfileByExePath(string path)
        {
            foreach (SteamProfile profile in _profiles.Values)
            {
                if (Program.PathsEqual(profile.ExePath, path))
                {
                    return profile;
                }
            }

            return null;
        }

        public static Settings Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("No se encontró el archivo de configuración.", path);
            }

            Dictionary<string, Dictionary<string, string>> sections =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> currentSection = null;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                {
                    string sectionName = line.Substring(1, line.Length - 2).Trim();
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[sectionName] = currentSection;
                    continue;
                }

                if (currentSection == null)
                {
                    continue;
                }

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim();
                string value = line.Substring(equalsIndex + 1).Trim();

                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[value.Length - 1] == '"') ||
                     (value[0] == '\'' && value[value.Length - 1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                currentSection[key] = value;
            }

            Settings settings = new Settings();

            Dictionary<string, string> general;
            if (sections.TryGetValue("General", out general))
            {
                settings.ConfirmSwitch = ParseBool(GetValue(general, "ConfirmSwitch"), false);
                settings.ShutdownTimeoutSeconds = ParseInt(GetValue(general, "ShutdownTimeoutSeconds"), 25, 5, 180);
                settings.StartupTimeoutSeconds = ParseInt(GetValue(general, "StartupTimeoutSeconds"), 25, 5, 180);
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                if (!section.Key.StartsWith("Profile:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string key = section.Key.Substring("Profile:".Length).Trim();
                if (key.Length == 0)
                {
                    throw new InvalidDataException("Hay una sección [Profile:] sin nombre en settings.ini.");
                }

                if (settings._profiles.ContainsKey(key))
                {
                    throw new InvalidDataException("El perfil '" + key + "' está duplicado en settings.ini.");
                }

                string exePath = GetValue(section.Value, "ExePath");
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    throw new InvalidDataException("El perfil '" + key + "' no tiene ExePath en settings.ini.");
                }

                string name = GetValue(section.Value, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = key;
                }

                string login = GetValue(section.Value, "Login");
                settings._profiles.Add(key, new SteamProfile(key, name.Trim(), exePath.Trim(), login.Trim()));
            }

            if (settings._profiles.Count == 0)
            {
                throw new InvalidDataException("settings.ini no contiene ningún perfil [Profile:...].");
            }

            return settings;
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;
            if (values.TryGetValue(key, out value))
            {
                return value;
            }

            return string.Empty;
        }

        private static int ParseInt(string value, int fallback, int minimum, int maximum)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                parsed = fallback;
            }

            if (parsed < minimum)
            {
                parsed = minimum;
            }

            if (parsed > maximum)
            {
                parsed = maximum;
            }

            return parsed;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string normalized = value.Trim();

            if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("1", StringComparison.Ordinal) ||
                normalized.Equals("si", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("sí", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("0", StringComparison.Ordinal) ||
                normalized.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fallback;
        }
    }
}
