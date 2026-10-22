using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;
using SteamSwitcher.Setup.Services;

namespace SteamSwitcher.Setup.ViewModels;

public enum InstallWizardPage
{
    Destinations,
    Progress,
    Done
}

public sealed partial class InstallWizardViewModel : ObservableObject
{
    private readonly SetupViewModel _owner;
    private readonly ValveInstallService _installer = new();
    private CancellationTokenSource? _cancellation;
    private bool _applied;
    private bool _installedA;
    private bool _installedB;

    [ObservableProperty]
    private InstallWizardPage _page = InstallWizardPage.Destinations;

    [ObservableProperty]
    private string _pathA = string.Empty;

    [ObservableProperty]
    private string _pathB = string.Empty;

    [ObservableProperty]
    private string _spaceA = string.Empty;

    [ObservableProperty]
    private string _spaceB = string.Empty;

    [ObservableProperty]
    private string _statusA = string.Empty;

    [ObservableProperty]
    private string _statusB = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressIndeterminate = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canCancel = true;

    public InstallWizardViewModel(SetupViewModel owner, BayViewModel? preselectedBay)
    {
        _owner = owner;
        _installedA = owner.BayA.IsInstalled;
        _installedB = owner.BayB.IsInstalled;

        PathA = SuggestPath(owner.BayA, owner.BayB, "A");
        PathB = SuggestPath(owner.BayB, owner.BayA, "B");
        if (PathB.Equals(PathA, StringComparison.OrdinalIgnoreCase))
        {
            PathB = SuggestAlternative(owner.BayA.DirectoryPath, "B");
        }

        StatusA = _installedA ? T("StateReady") : T("StatePending");
        StatusB = _installedB ? T("StateReady") : T("StatePending");
        UpdateSpaces();

        if (preselectedBay == owner.BayB && !_installedB)
        {
            // Preselection only changes the visual focus; both copies follow the same flow.
        }
    }

    public event EventHandler? Finished;

    public void Abort()
    {
        _cancellation?.Cancel();
        Finished = null;
    }

    public string StepLabel => Page switch
    {
        InstallWizardPage.Destinations => "01 / 03",
        InstallWizardPage.Progress => "02 / 03",
        _ => "03 / 03"
    };

    public bool IsDestinations => Page == InstallWizardPage.Destinations;
    public bool IsProgress => Page == InstallWizardPage.Progress;
    public bool IsDone => Page == InstallWizardPage.Done;
    public bool IsInstalledA => _installedA;
    public bool IsInstalledB => _installedB;
    public bool NeedsA => !_installedA;
    public bool NeedsB => !_installedB;
    public bool CanEditA => !_installedA && !IsBusy;
    public bool CanEditB => !_installedB && !IsBusy;
    public bool CanStart => !IsBusy && (NeedsA || NeedsB);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
    public string ActionLabel => Page == InstallWizardPage.Done
        ? T("InstallWizardBack")
        : IsBusy && CanCancel
            ? T("ActionCancel")
            : T("InstallWizardStart");
    public string CloseLabel => IsBusy ? T("ActionCancel") : T("ActionClose");
    public string DoneActionLabel => _installedA && _installedB
        ? T("WizardDoneContinue")
        : T("InstallWizardBack");

    partial void OnPageChanged(InstallWizardPage value)
    {
        OnPropertyChanged(nameof(IsDestinations));
        OnPropertyChanged(nameof(IsProgress));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(CloseLabel));
    }

    partial void OnPathAChanged(string value)
    {
        UpdateSpaces();
        OnPropertyChanged(nameof(CanStart));
    }

    partial void OnPathBChanged(string value)
    {
        UpdateSpaces();
        OnPropertyChanged(nameof(CanStart));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditA));
        OnPropertyChanged(nameof(CanEditB));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(CloseLabel));
    }

    partial void OnCanCancelChanged(bool value)
    {
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(CloseLabel));
    }

    partial void OnErrorTextChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand]
    private void ChooseA()
    {
        if (!CanEditA)
        {
            return;
        }

        var selected = ChooseFolder(PathA);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            PathA = selected;
        }
    }

    [RelayCommand]
    private void ChooseB()
    {
        if (!CanEditB)
        {
            return;
        }

        var selected = ChooseFolder(PathB);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            PathB = selected;
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Validate(out var validationMessage))
        {
            ErrorText = validationMessage;
            return;
        }

        if (!NeedsA && !NeedsB)
        {
            Page = InstallWizardPage.Done;
            return;
        }

        ErrorText = null;
        IsBusy = true;
        Page = InstallWizardPage.Progress;
        IsProgressIndeterminate = true;
        ProgressValue = 0;
        CanCancel = true;
        StatusMessage = T("InstallWizardProgressDesc");
        StatusA = _installedA ? T("StateReady") : T("StatePending");
        StatusB = _installedB ? T("StateReady") : T("StatePending");

        _cancellation = new CancellationTokenSource();
        try
        {
            var targets = new List<SteamInstallTarget>
            {
                new("A", PathA, _installedA),
                new("B", PathB, _installedB)
            };

            var progress = new Progress<SteamInstallProgress>(OnProgress);
            var result = await _installer.InstallAsync(targets, progress, _cancellation.Token);
            if (result.Success)
            {
                _installedA = result.ExePathFor("A") is not null || _installedA;
                _installedB = result.ExePathFor("B") is not null || _installedB;
                StatusA = _installedA ? T("StateReady") : T("StatePending");
                StatusB = _installedB ? T("StateReady") : T("StatePending");
                NotifyInstallStateChanged();
                StatusMessage = T("InstallWizardDoneDesc");
                Page = InstallWizardPage.Done;
                return;
            }

            _installedA = result.ExePathFor("A") is not null || _installedA;
            _installedB = result.ExePathFor("B") is not null || _installedB;
            StatusA = _installedA ? T("StateReady") : T("StatePending");
            StatusB = _installedB ? T("StateReady") : T("StatePending");
            NotifyInstallStateChanged();

            if (_cancellation.IsCancellationRequested)
            {
                Page = InstallWizardPage.Destinations;
                StatusMessage = T("StatusValveCancelled");
                return;
            }

            ErrorText = result.Message;
            StatusMessage = T("InstallWizardError");
            CanCancel = true;
        }
        catch (OperationCanceledException)
        {
            Page = InstallWizardPage.Destinations;
            StatusMessage = T("StatusValveCancelled");
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusMessage = T("InstallWizardError");
            CanCancel = true;
        }
        finally
        {
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
            OnPropertyChanged(nameof(Page));
            OnPropertyChanged(nameof(IsDestinations));
            OnPropertyChanged(nameof(IsProgress));
            OnPropertyChanged(nameof(IsDone));
            OnPropertyChanged(nameof(StepLabel));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsBusy)
        {
            if (CanCancel)
            {
                _cancellation?.Cancel();
            }

            return;
        }

        Complete(applyInstalledPaths: true);
    }

    [RelayCommand]
    private void Finish()
    {
        if (IsBusy)
        {
            return;
        }

        Complete(applyInstalledPaths: true);
    }

    private void Complete(bool applyInstalledPaths)
    {
        if (!_applied && applyInstalledPaths && (_installedA || _installedB))
        {
            _applied = true;
            _owner.ApplyInstalledBays(
                _installedA ? PathA : null,
                _installedB ? PathB : null);
        }

        Finished?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Retry()
    {
        ErrorText = null;
        _ = StartAsync();
    }

    private void OnProgress(SteamInstallProgress progress)
    {
        StatusMessage = progress.Message;
        if (progress.Stage == SteamInstallStage.Downloading && progress.Fraction is not null)
        {
            CanCancel = true;
            IsProgressIndeterminate = false;
            ProgressValue = Math.Clamp(progress.Fraction.Value * 55, 0, 55);
        }
        else if (progress.Stage == SteamInstallStage.Installing)
        {
            CanCancel = false;
            IsProgressIndeterminate = true;
            var total = Math.Max(1, progress.TotalTargets);
            ProgressValue = 55 + (45d * progress.CompletedTargets / total);
            if (progress.Slot == "A")
            {
                StatusA = T("StateInstalling");
            }
            else if (progress.Slot == "B")
            {
                StatusB = T("StateInstalling");
            }
        }
        else if (progress.Stage == SteamInstallStage.VerifyingInstall)
        {
            CanCancel = false;
            IsProgressIndeterminate = true;
            if (progress.Slot == "A")
            {
                StatusA = T("StateVerifying");
            }
            else if (progress.Slot == "B")
            {
                StatusB = T("StateVerifying");
            }
        }
        else if (progress.Stage == SteamInstallStage.Completed)
        {
            CanCancel = true;
            IsProgressIndeterminate = false;
            ProgressValue = 100;
        }
        else
        {
            CanCancel = progress.Stage is not SteamInstallStage.VerifyingInstaller and not SteamInstallStage.ClosingSteam;
            IsProgressIndeterminate = true;
            ProgressValue = Math.Max(ProgressValue, 2);
        }
    }

    private void NotifyInstallStateChanged()
    {
        OnPropertyChanged(nameof(IsInstalledA));
        OnPropertyChanged(nameof(IsInstalledB));
        OnPropertyChanged(nameof(NeedsA));
        OnPropertyChanged(nameof(NeedsB));
        OnPropertyChanged(nameof(CanEditA));
        OnPropertyChanged(nameof(CanEditB));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(DoneActionLabel));
    }

    private bool Validate(out string message)
    {
        if (!NeedsA && !NeedsB)
        {
            message = string.Empty;
            return true;
        }

        if (NeedsA && string.IsNullOrWhiteSpace(PathA))
        {
            message = T("InstallWizardPathRequired");
            return false;
        }

        if (NeedsB && string.IsNullOrWhiteSpace(PathB))
        {
            message = T("InstallWizardPathRequired");
            return false;
        }

        var pathA = Normalize(PathA);
        var pathB = Normalize(PathB);
        if (NeedsA && NeedsB && pathA.Equals(pathB, StringComparison.OrdinalIgnoreCase))
        {
            message = T("InstallWizardSamePath");
            return false;
        }

        if (NeedsA && NeedsB && AreNested(pathA, pathB))
        {
            message = T("ValidationNestedDirectory");
            return false;
        }

        foreach (var (path, needed) in new[] { (pathA, NeedsA), (pathB, NeedsB) })
        {
            if (!needed)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(path) || path.Contains('"'))
            {
                message = T("InstallWizardPathRequired");
                return false;
            }

            try
            {
                _ = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        if (NeedsA && File.Exists(Path.Combine(pathA, "steam.exe")))
        {
            _installedA = true;
            StatusA = T("StateReady");
            OnPropertyChanged(nameof(IsInstalledA));
            OnPropertyChanged(nameof(NeedsA));
        }

        if (NeedsB && File.Exists(Path.Combine(pathB, "steam.exe")))
        {
            _installedB = true;
            StatusB = T("StateReady");
            OnPropertyChanged(nameof(IsInstalledB));
            OnPropertyChanged(nameof(NeedsB));
        }

        NotifyInstallStateChanged();
        message = string.Empty;
        return true;
    }

    private void UpdateSpaces()
    {
        SpaceA = GetFreeSpace(PathA);
        SpaceB = GetFreeSpace(PathB);
    }

    private string GetFreeSpace(string? path)
    {
        try
        {
            var root = string.IsNullOrWhiteSpace(path)
                ? Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))
                : Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return string.Empty;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady
                ? LocalizationService.T("InstallWizardSpace", FormatSize(drive.AvailableFreeSpace))
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FormatSize(long bytes)
    {
        const double mega = 1024d * 1024d;
        return bytes >= 1024d * mega
            ? $"{bytes / (1024d * mega):0.#} GB"
            : $"{bytes / mega:0} MB";
    }

    private static string ChooseFolder(string? initial)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("ChooseFolder"),
            InitialDirectory = !string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial)
                ? initial
                : string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : string.Empty;
    }

    private static string SuggestPath(BayViewModel bay, BayViewModel other, string slot)
    {
        var candidate = bay.DirectoryPath;
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Equals(other.DirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            candidate = SuggestAlternative(other.DirectoryPath, slot);
        }

        return Normalize(candidate);
    }

    private static string SuggestAlternative(string? other, string slot)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        if (string.IsNullOrWhiteSpace(root))
        {
            root = "C:\\";
        }

        var folder = slot == "A" ? "Steam_A" : "Steam_B";
        var preferred = Path.Combine(root, folder);
        if (!preferred.Equals(other, StringComparison.OrdinalIgnoreCase))
        {
            return preferred;
        }

        return Path.Combine(root, folder + "_2");
    }

    private static bool AreNested(string pathA, string pathB)
    {
        var a = pathA.TrimEnd('\\');
        var b = pathB.TrimEnd('\\');
        return a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase) ||
               b.StartsWith(a + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? path)
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

    private static string T(string key) => LocalizationService.T(key);
}
