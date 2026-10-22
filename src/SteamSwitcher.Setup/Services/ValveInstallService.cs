using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.Services;

public sealed class ValveInstallService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private static readonly string[] SteamSetupUrls =
    [
        "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe",
        "https://cdn.fastly.steamstatic.com/client/installer/SteamSetup.exe"
    ];

    private readonly SteamLaunchService _launchService = new();

    public async Task<SteamInstallResult> InstallAsync(
        IReadOnlyList<SteamInstallTarget> targets,
        IProgress<SteamInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SteamInstallTargetResult>();
        var pending = new List<SteamInstallTarget>();

        foreach (var target in targets)
        {
            var normalized = NormalizeDirectory(target.Path);
            var exePath = string.IsNullOrWhiteSpace(normalized)
                ? null
                : Path.Combine(normalized, "steam.exe");

            if (target.AlreadyInstalled || (exePath is not null && File.Exists(exePath)))
            {
                results.Add(new SteamInstallTargetResult(
                    target.Slot,
                    normalized ?? target.Path,
                    true,
                    exePath,
                    LocalizationService.T("StatusValveAlreadyInstalled")));
            }
            else
            {
                pending.Add(target with { Path = normalized ?? target.Path });
            }
        }

        if (pending.Count == 0)
        {
            return new SteamInstallResult(
                true,
                LocalizationService.T("StatusValveInstalled"),
                results);
        }

        string? setupPath = null;
        try
        {
            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.Preparing,
                LocalizationService.T("StatusPreparingInstall", "Steam"),
                null,
                0,
                results.Count,
                targets.Count));

            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.ClosingSteam,
                LocalizationService.T("StatusValveClosingSteam"),
                null,
                null,
                results.Count,
                targets.Count));

            var stopResult = await _launchService.StopAllAsync(20, cancellationToken);
            if (!stopResult.Success)
            {
                return Failure(stopResult.Message);
            }

            setupPath = Path.Combine(
                Path.GetTempPath(),
                $"SteamSwitcher-SteamSetup-{Guid.NewGuid():N}.exe");

            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.Downloading,
                LocalizationService.T("StatusValveDownloading"),
                null,
                0,
                results.Count,
                targets.Count));

            if (!await DownloadSetupAsync(setupPath, progress, results.Count, targets.Count, cancellationToken))
            {
                return Failure(LocalizationService.T("StatusValveDownloadFailed"));
            }

            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.VerifyingInstaller,
                LocalizationService.T("StatusValveVerifying"),
                null,
                null,
                results.Count,
                targets.Count));

            if (!IsValveSigned(setupPath))
            {
                return Failure(LocalizationService.T("StatusValveSignatureInvalid"));
            }

            foreach (var target in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (results.Any(result => result.Success && result.Slot != target.Slot))
                {
                    progress?.Report(new SteamInstallProgress(
                        SteamInstallStage.ClosingSteam,
                        LocalizationService.T("StatusValveClosingSteam"),
                        target.Slot,
                        null,
                        results.Count(result => result.Success),
                        targets.Count));

                    var secondStop = await _launchService.StopAllAsync(20, cancellationToken);
                    if (!secondStop.Success)
                    {
                        return Failure(secondStop.Message, results);
                    }
                }

                var result = await InstallTargetAsync(
                    setupPath,
                    target,
                    progress,
                    results.Count,
                    targets.Count,
                    cancellationToken);

                results.Add(result);
            }

            var success = results.All(result => result.Success);
            var message = success
                ? LocalizationService.T("StatusValveInstalled")
                : LocalizationService.T("StatusValveMissingExe");

            progress?.Report(new SteamInstallProgress(
                success ? SteamInstallStage.Completed : SteamInstallStage.Failed,
                message,
                null,
                success ? 1 : null,
                results.Count(result => result.Success),
                targets.Count));

            return new SteamInstallResult(success, message, results);
        }
        catch (OperationCanceledException)
        {
            return Failure(LocalizationService.T("StatusValveCancelled"), results);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, results);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(setupPath))
            {
                try
                {
                    if (File.Exists(setupPath))
                    {
                        File.Delete(setupPath);
                    }
                }
                catch
                {
                    // A signed installer that cannot be deleted must not break the flow.
                }
            }
        }
    }

    private async Task<SteamInstallTargetResult> InstallTargetAsync(
        string setupPath,
        SteamInstallTarget target,
        IProgress<SteamInstallProgress>? progress,
        int completedTargets,
        int totalTargets,
        CancellationToken cancellationToken)
    {
        var directory = NormalizeDirectory(target.Path);
        var targetExe = Path.Combine(directory, "steam.exe");

        progress?.Report(new SteamInstallProgress(
            SteamInstallStage.Installing,
            LocalizationService.T("StatusValveInstalling"),
            target.Slot,
            null,
            completedTargets,
            totalTargets));

        try
        {
            var arguments = $"/S /D={directory}";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                return new SteamInstallTargetResult(
                    target.Slot,
                    directory,
                    false,
                    null,
                    LocalizationService.T("StatusValveStartFailed"));
            }

            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.Installing,
                LocalizationService.T("StatusValveOpeningUac"),
                target.Slot,
                null,
                completedTargets,
                totalTargets));

            // Once the elevated installer has started, it cannot be safely cancelled.
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return new SteamInstallTargetResult(
                    target.Slot,
                    directory,
                    false,
                    null,
                    LocalizationService.T("StatusValveExitCode", process.ExitCode));
            }

            progress?.Report(new SteamInstallProgress(
                SteamInstallStage.VerifyingInstall,
                LocalizationService.T("StatusValveVerifyingInstall"),
                target.Slot,
                null,
                completedTargets,
                totalTargets));

            for (var attempt = 0; attempt < 90; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(targetExe))
                {
                    progress?.Report(new SteamInstallProgress(
                        SteamInstallStage.Installing,
                        LocalizationService.T("StatusValveInstalled"),
                        target.Slot,
                        null,
                        completedTargets + 1,
                        totalTargets));

                    return new SteamInstallTargetResult(
                        target.Slot,
                        directory,
                        true,
                        targetExe,
                        LocalizationService.T("StatusValveInstalled"));
                }

                await Task.Delay(500, cancellationToken);
            }

            return new SteamInstallTargetResult(
                target.Slot,
                directory,
                false,
                null,
                LocalizationService.T("StatusValveMissingExe"));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new SteamInstallTargetResult(
                target.Slot,
                directory,
                false,
                null,
                LocalizationService.T("StatusValveUacCancelled"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SteamInstallTargetResult(
                target.Slot,
                directory,
                false,
                null,
                ex.Message);
        }
    }

    private static async Task<bool> DownloadSetupAsync(
        string destination,
        IProgress<SteamInstallProgress>? progress,
        int completedTargets,
        int totalTargets,
        CancellationToken cancellationToken)
    {
        foreach (var url in SteamSetupUrls)
        {
            try
            {
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var total = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(destination);
                var buffer = new byte[81920];
                var readTotal = 0L;
                var lastReported = -1;

                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    readTotal += read;
                    if (total is > 0)
                    {
                        var percent = (int)(readTotal * 100 / total.Value);
                        if (percent != lastReported && (percent % 2 == 0 || percent == 100))
                        {
                            lastReported = percent;
                            progress?.Report(new SteamInstallProgress(
                                SteamInstallStage.Downloading,
                                LocalizationService.T("StatusValveDownloadPercent", percent),
                                null,
                                percent / 100d,
                                completedTargets,
                                totalTargets));
                        }
                    }
                }

                return File.Exists(destination) && new FileInfo(destination).Length > 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                progress?.Report(new SteamInstallProgress(
                    SteamInstallStage.Downloading,
                    LocalizationService.T("StatusValveServerFailed", ex.Message),
                    null,
                    null,
                    completedTargets,
                    totalTargets));
            }
        }

        return false;
    }

    private static SteamInstallResult Failure(
        string message,
        IReadOnlyList<SteamInstallTargetResult>? existing = null)
    {
        return new SteamInstallResult(false, message, existing ?? []);
    }

    private static string NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var candidate = path.Trim().Trim('"').Replace('/', '\\');
        if (candidate.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.GetDirectoryName(candidate) ?? candidate;
        }

        if (candidate.Length > 3)
        {
            candidate = candidate.TrimEnd('\\');
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch
        {
            return candidate;
        }
    }

    private static bool IsValveSigned(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NonInteractive");
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
            process.StartInfo.ArgumentList.Add("-Command");
            process.StartInfo.ArgumentList.Add(
                "$s=Get-AuthenticodeSignature -LiteralPath $env:STEAM_SETUP_FILE; " +
                "if ($s.Status -ne 'Valid') { exit 1 }; " +
                "if ($s.SignerCertificate.Subject -notmatch 'Valve') { exit 2 }");
            process.StartInfo.Environment["STEAM_SETUP_FILE"] = path;
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
