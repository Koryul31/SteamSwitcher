using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;
using SteamSwitcher.Setup.Services;

namespace SteamSwitcher.Setup.ViewModels;

public sealed partial class SetupViewModel : ObservableObject
{
    private readonly SteamDiscoveryService _discovery = new();
    private readonly SteamAccountService _accountService = new();
    private readonly SteamLaunchService _launchService = new();
    private readonly ShortcutService _shortcutService = new();
    private readonly Core.Services.SteamDiscoveryService _coreDiscovery = new();
    private readonly Core.Services.SteamAccountService _coreAccountService = new();
    private readonly Core.Services.RegistryService _coreRegistry = new();
    private readonly Core.Services.SteamProcessService _coreProcess = new();
    private readonly Core.Services.ShortcutService _coreShortcut = new();
    private readonly Core.Services.SteamSwitchService _coreSwitch;

    public ObservableCollection<Core.Models.SteamAccount> AllDiscoveredAccounts { get; } = [];

    [ObservableProperty]
    private bool _isSingleSteamMode = true;

    public bool IsDualSteamMode
    {
        get => !IsSingleSteamMode;
        set => IsSingleSteamMode = !value;
    }

    public string ModeTitle => IsSingleSteamMode
        ? LocalizationService.T("ModeOneSteamTitle")
        : LocalizationService.T("ModeTwoSteamTitle");

    public string ModeBadge => IsSingleSteamMode
        ? LocalizationService.T("ModeOneSteamTag")
        : LocalizationService.T("ModeTwoSteamTag");

    public string ModeDescription => IsSingleSteamMode
        ? LocalizationService.T("ModeOneSteamDesc")
        : LocalizationService.T("ModeTwoSteamDesc");

    public string AccountSectionTitle => IsSingleSteamMode
        ? LocalizationService.T("AccountsSlotTitleSingle")
        : LocalizationService.T("AccountsSlotTitleDual");

    [RelayCommand]
    private void SelectSingleSteamMode()
    {
        IsSingleSteamMode = true;
    }

    [RelayCommand]
    private void SelectDualSteamMode()
    {
        IsSingleSteamMode = false;
    }

    partial void OnIsSingleSteamModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDualSteamMode));
        OnPropertyChanged(nameof(NeedsInstallation));
        OnPropertyChanged(nameof(InstallationsStatus));
        OnPropertyChanged(nameof(ModeTitle));
        OnPropertyChanged(nameof(ModeBadge));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(AccountSectionTitle));

        BayA.IsSingleSteamMode = value;
        BayB.IsSingleSteamMode = value;

        if (value)
        {
            if (BayA.IsInstalled)
            {
                BayB.SetDirectory(BayA.DirectoryPath);
                RefreshAccountsInternal(BayA.SelectedAccount?.AccountName, BayB.SelectedAccount?.AccountName);
            }
        }
        else
        {
            if (SameDirectory())
            {
                BayB.SetDirectory(string.Empty);
            }
        }

        MarkDirty();
        UpdateSummaries();
    }
    private readonly BackendConfiguration _backend;
    private readonly SetupContext _context;
    private SetupPreferences _preferences;
    private bool _suppressDirty;
    private int? _managedShortcutCount;

    [ObservableProperty]
    private bool _createDesktop = true;

    [ObservableProperty]
    private bool _createStartMenu = true;

    [ObservableProperty]
    private bool _confirmSwitch = true;

    [ObservableProperty]
    private int _shutdownTimeoutSeconds = 20;

    [ObservableProperty]
    private string _statusMessage = LocalizationService.T("StatusLoaded");

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _requiredStep;

    [ObservableProperty]
    private bool _applyCompleted;

    [ObservableProperty]
    private SetupThemeMode _themeMode = SetupThemeMode.System;

    [ObservableProperty]
    private string _languageSetting = "System";

    [ObservableProperty]
    private ShortcutLayoutMode _shortcutLayout = ShortcutLayoutMode.Folder;

    public SetupViewModel()
    {
        _context = SetupContext.Locate();
        _backend = BackendConfiguration.Load(_context);
        _preferences = SetupPreferences.Load(_context);

        BayA = new BayViewModel("A", "#7BE0B0");
        BayB = new BayViewModel("B", "#86A9FF");
        ShortcutModeOptions = BuildShortcutModeOptions();
        ShortcutLayoutOptions = BuildShortcutLayoutOptions();
        ThemeOptions = BuildThemeOptions();
        LanguageOptions = BuildLanguageOptions();

        BayA.PropertyChanged += Bay_PropertyChanged;
        BayB.PropertyChanged += Bay_PropertyChanged;
        LocalizationService.LanguageChanged += Localization_LanguageChanged;
        _coreSwitch = new Core.Services.SteamSwitchService(_coreDiscovery, _coreAccountService, _coreRegistry, _coreProcess);

        LoadState();
        RefreshAllAccounts();
    }

    public BayViewModel BayA { get; }
    public BayViewModel BayB { get; }
    public IReadOnlyList<ShortcutModeOption> ShortcutModeOptions { get; private set; }
    public IReadOnlyList<ShortcutLayoutOption> ShortcutLayoutOptions { get; private set; }
    public IReadOnlyList<ThemeOption> ThemeOptions { get; private set; }
    public IReadOnlyList<LanguageOption> LanguageOptions { get; private set; }
    public SetupContext Context => _context;
    public string RootDirectory => _context.RootDirectory;
    public string BackendPath => _context.BackendPath;
    public string SettingsPath => _context.SettingsPath;
    public string SetupPath => _context.SetupPath;
    public string Version => LocalizationService.T("VersionText");
    public int InitialStepIndex { get; private set; }
    public bool ShouldShowWelcome =>
        !_preferences.WelcomeCompleted &&
        !_preferences.Exists &&
        !_context.HasSettings;
    public string BackendStatus => _context.HasBackend
        ? LocalizationService.T("BackendConnected")
        : LocalizationService.T("BackendMissing");
    public string ShortcutLayoutDescription => ShortcutLayout == ShortcutLayoutMode.Folder
        ? LocalizationService.T("LayoutFolder")
        : LocalizationService.T("LayoutDirect");
    public string ShortcutLocationsPreview
    {
        get
        {
            var locations = new List<string>();
            if (CreateDesktop)
            {
                locations.Add(ShortcutLayout == ShortcutLayoutMode.Folder
                    ? LocalizationService.T("LocationDesktopFolder")
                    : LocalizationService.T("LocationDesktopDirect"));
            }

            if (CreateStartMenu)
            {
                locations.Add(ShortcutLayout == ShortcutLayoutMode.Folder
                    ? LocalizationService.T("LocationStartFolder")
                    : LocalizationService.T("LocationStartDirect"));
            }

            return locations.Count == 0
                ? LocalizationService.T("LocationNone")
                : string.Join(" � ", locations);
        }
    }

    private static IReadOnlyList<ShortcutModeOption> BuildShortcutModeOptions() =>
    [
        new ShortcutModeOption(ShortcutMode.None, LocalizationService.T("ShortcutNone")),
        new ShortcutModeOption(ShortcutMode.Normal, LocalizationService.T("ShortcutNormal")),
        new ShortcutModeOption(ShortcutMode.BigPicture, LocalizationService.T("ShortcutBigPicture")),
        new ShortcutModeOption(ShortcutMode.Both, LocalizationService.T("ShortcutBoth"))
    ];

    private static IReadOnlyList<ShortcutLayoutOption> BuildShortcutLayoutOptions() =>
    [
        new ShortcutLayoutOption(ShortcutLayoutMode.Folder, LocalizationService.T("LayoutFolder")),
        new ShortcutLayoutOption(ShortcutLayoutMode.Direct, LocalizationService.T("LayoutDirect"))
    ];

    private static IReadOnlyList<ThemeOption> BuildThemeOptions() =>
    [
        new ThemeOption(SetupThemeMode.System, LocalizationService.T("ThemeSystem")),
        new ThemeOption(SetupThemeMode.Light, LocalizationService.T("ThemeLight")),
        new ThemeOption(SetupThemeMode.Dark, LocalizationService.T("ThemeDark"))
    ];

    private static IReadOnlyList<LanguageOption> BuildLanguageOptions()
    {
        var systemLabel = string.Format(
            LocalizationService.T("LanguageSystemNamed"),
            LocalizationService.Instance.SystemNativeName);
        var options = new List<LanguageOption> { new("System", systemLabel) };

        options.AddRange(LocalizationService.SupportedLanguages.Select(language =>
            new LanguageOption(language.Code, language.NativeName)));
        return options;
    }

    private void Localization_LanguageChanged()
    {
        foreach (var option in ShortcutModeOptions)
        {
            option.Label = option.Value switch
            {
                ShortcutMode.None => LocalizationService.T("ShortcutNone"),
                ShortcutMode.Normal => LocalizationService.T("ShortcutNormal"),
                ShortcutMode.BigPicture => LocalizationService.T("ShortcutBigPicture"),
                _ => LocalizationService.T("ShortcutBoth")
            };
        }

        foreach (var option in ShortcutLayoutOptions)
        {
            option.Label = option.Value == ShortcutLayoutMode.Folder
                ? LocalizationService.T("LayoutFolder")
                : LocalizationService.T("LayoutDirect");
        }

        foreach (var option in ThemeOptions)
        {
            option.Label = option.Value switch
            {
                SetupThemeMode.System => LocalizationService.T("ThemeSystem"),
                SetupThemeMode.Light => LocalizationService.T("ThemeLight"),
                _ => LocalizationService.T("ThemeDark")
            };
        }

        var systemOption = LanguageOptions.FirstOrDefault(option => option.Value == "System");
        if (systemOption is not null)
        {
            systemOption.Label = string.Format(
                LocalizationService.T("LanguageSystemNamed"),
                LocalizationService.Instance.SystemNativeName);
        }

        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(ModeTitle));
        OnPropertyChanged(nameof(ModeBadge));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(AccountSectionTitle));
        OnPropertyChanged(nameof(MultiInstallNotice));

        BayA.RefreshLocalization();
        BayB.RefreshLocalization();

        if (!_suppressDirty)
        {
            StatusMessage = LocalizationService.T("StatusLanguageChanged");
        }

        UpdateSummaries();
    }
    public string ShortcutSummary =>
        $"{BayA.NameDisplay}: {BayA.ShortcutModeLabel} � {BayB.NameDisplay}: {BayB.ShortcutModeLabel}";
    public string InstallationsStatus =>
        IsSingleSteamMode
            ? (BayA.IsInstalled ? LocalizationService.T("StateReady") : LocalizationService.T("StatePending"))
            : (BayA.IsInstalled && BayB.IsInstalled && !SameDirectory() ? LocalizationService.T("StateReady") : LocalizationService.T("StatePending"));

    public bool NeedsInstallation =>
        IsSingleSteamMode
            ? !BayA.IsInstalled
            : (!BayA.IsInstalled || !BayB.IsInstalled);
    public string AccountsStatus =>
        BayA.SelectedAccount is not null && BayB.SelectedAccount is not null &&
        !SameAccount()
            ? (BothAccountsRemembered ? LocalizationService.T("StateReady") : LocalizationService.T("StateReview"))
            : LocalizationService.T("StatePending");
    public string ShortcutsStatus
    {
        get
        {
            var expected = ExpectedShortcutCount;
            var actual = ManagedShortcutCount;
            if (expected == 0)
            {
                return actual == 0 ? LocalizationService.T("NoShortcuts") : LocalizationService.T("StateReview");
            }

            return actual == expected ? LocalizationService.T("StateReady") : LocalizationService.T("StatePending");
        }
    }
    public string OptionsStatus => LocalizationService.T("StateOptional");
    public string SummaryStatus => ApplyCompleted || IsFullyConfigured
        ? LocalizationService.T("SummaryReadyTitle")
        : LocalizationService.T("SummaryReviewTitle");
    public bool BothAccountsRemembered => BayA.IsAccountReady && BayB.IsAccountReady;
    public string PairStatusTitle => BothAccountsRemembered
        ? LocalizationService.T("PairReadyTitle")
        : LocalizationService.T("PairProgressTitle");
    public string PairStatusDetail => BothAccountsRemembered
        ? LocalizationService.T("PairReadyDetail")
        : LocalizationService.T("PairProgressDetail");
    public bool HasSavedConfiguration => _context.HasSettings && _backend.Profiles.Count >= 2;
        public bool ShortcutsReady
    {
        get
        {
            var expected = ExpectedShortcutCount;
            var actual = ManagedShortcutCount;
            return expected == 0 ? actual == 0 : actual == expected;
        }
    }
    public bool IsFullyConfigured =>
        HasSavedConfiguration &&
        (IsSingleSteamMode ? BayA.IsInstalled : (BayA.IsInstalled && BayB.IsInstalled)) &&
        BayA.SelectedAccount is not null && BayB.SelectedAccount is not null &&
        !SameAccount() && BothAccountsRemembered &&
        _context.HasBackend && ShortcutsReady;
    public int ExpectedShortcutCount
    {
        get
        {
            var destinationCount = (CreateDesktop ? 1 : 0) + (CreateStartMenu ? 1 : 0);
            return destinationCount * (CountMode(BayA.ShortcutMode) + CountMode(BayB.ShortcutMode));
        }
    }

    private int ManagedShortcutCount =>
        _managedShortcutCount ??= _shortcutService.CountManagedLinks(ShortcutLayout, CreateDesktop, CreateStartMenu);

    public void RefreshShortcutState()
    {
        _managedShortcutCount = _shortcutService.CountManagedLinks(ShortcutLayout, CreateDesktop, CreateStartMenu);
        UpdateSummaries();
    }

    public BayViewModel? GetPendingAccountBay()
    {
        if (BayA.SelectedAccount is null || !BayA.IsAccountReady)
        {
            return BayA;
        }

        if (BayB.SelectedAccount is null || !BayB.IsAccountReady)
        {
            return BayB;
        }

        return null;
    }

    public bool CanContinueFrom(int step, out string message)
    {
        switch (step)
        {
            case 0:
                return ValidateInstallations(out message);
            case 1:
                return ValidateAccounts(out message);
            case 2:
                return ValidateAccess(out message);
            default:
                message = string.Empty;
                return true;
        }
    }

    public bool CanNavigateTo(int targetStep, out string message)
    {
        message = string.Empty;
        if (targetStep <= 0)
        {
            return true;
        }

        // To jump forward, every previous step must be valid. This lets a
        // direct click move ahead without skipping the flow validation.
        for (var step = 0; step < targetStep; step++)
        {
            if (!CanContinueFrom(step, out message))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateInstallations(out string message)
    {
        if (IsSingleSteamMode)
        {
            if (!BayA.IsInstalled)
            {
                message = LocalizationService.T("ValidationNeedInstalls");
                return false;
            }
            message = string.Empty;
            return true;
        }

        if (!BayA.IsInstalled || !BayB.IsInstalled)
        {
            message = LocalizationService.T("ValidationNeedInstalls");
            return false;
        }

        if (SameDirectory())
        {
            message = LocalizationService.T("ValidationSameDirectory");
            return false;
        }

        if (AreNestedDirectories())
        {
            message = LocalizationService.T("ValidationNestedDirectory");
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool ValidateAccounts(out string message)
    {
        if (BayA.SelectedAccount is null || BayB.SelectedAccount is null)
        {
            message = LocalizationService.T("ValidationNeedAccounts");
            return false;
        }

        if (SameAccount())
        {
            message = LocalizationService.T("ValidationSameAccount");
            return false;
        }

        if (!BothAccountsRemembered)
        {
            message = LocalizationService.T("WarningRemember");
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool ValidateAccess(out string message)
    {
        if ((CreateDesktop || CreateStartMenu) &&
            BayA.NameDisplay.Equals(BayB.NameDisplay, StringComparison.OrdinalIgnoreCase))
        {
            message = LocalizationService.T("ValidationSameShortcutName");
            return false;
        }

        message = string.Empty;
        return true;
    }

    public void MarkWelcomeCompleted()
    {
        if (_preferences.WelcomeCompleted)
        {
            return;
        }

        try
        {
            _preferences.MarkWelcomeCompleted(_context);
        }
        catch (IOException)
        {
            // Welcome completion must not block setup if the marker cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
            // Welcome completion must not block setup if the marker cannot be written.
        }
    }

    public string WarningText
    {
        get
        {
            if (IsFullyConfigured)
            {
                return LocalizationService.T("WarningReady");
            }

            var warnings = new List<string>();
            if (!HasSavedConfiguration)
            {
                warnings.Add(LocalizationService.T("WarningNoConfig"));
            }
            if (IsSingleSteamMode ? !BayA.IsInstalled : (!BayA.IsInstalled || !BayB.IsInstalled))
            {
                warnings.Add(LocalizationService.T("WarningMissingInstall"));
            }
            if (BayA.SelectedAccount is null || BayB.SelectedAccount is null)
            {
                warnings.Add(LocalizationService.T("WarningMissingLogin"));
            }
            else if (SameAccount())
            {
                warnings.Add(LocalizationService.T("WarningSameAccount"));
            }
            else if (!BothAccountsRemembered)
            {
                warnings.Add(LocalizationService.T("WarningRemember"));
            }
            if (!_context.HasBackend)
            {
                warnings.Add(LocalizationService.T("WarningNoBackend"));
            }
            if (ExpectedShortcutCount > 0 && !ShortcutsReady)
            {
                warnings.Add(LocalizationService.T("WarningMissingShortcuts"));
            }

            return warnings.Count == 0
                ? LocalizationService.T("WarningPrepared")
                : string.Join(" ", warnings);
        }
    }

    public void LoadState()
    {
        _suppressDirty = true;
        try
        {
            var discovery = _discovery.Discover(_backend.Profiles.Select(profile => profile.ExePath));
            var profileA = ResolveProfile(_preferences.ProfileAKey, 0);
            var profileB = ResolveProfile(_preferences.ProfileBKey, 1);

            BayA.ProfileKey = string.IsNullOrWhiteSpace(profileA?.Key) ? "steam1" : profileA.Key;
            BayB.ProfileKey = string.IsNullOrWhiteSpace(profileB?.Key) ? "steam2" : profileB.Key;

            BayA.Name = string.IsNullOrWhiteSpace(profileA?.Name) ? string.Empty : profileA.Name;
            BayB.Name = string.IsNullOrWhiteSpace(profileB?.Name) ? string.Empty : profileB.Name;

            BayA.SetDirectory(string.IsNullOrWhiteSpace(profileA?.ExePath) ? string.Empty : profileA.ExePath);
            BayB.SetDirectory(string.IsNullOrWhiteSpace(profileB?.ExePath) ? string.Empty : profileB.ExePath);

            IsSingleSteamMode = _preferences.IsSingleSteamMode;
            BayA.IsSingleSteamMode = IsSingleSteamMode;
            BayB.IsSingleSteamMode = IsSingleSteamMode;

            if (string.IsNullOrWhiteSpace(BayA.DirectoryPath) && discovery.Count > 0)
            {
                BayA.SetDirectory(discovery[0].ExePath);
            }

            if (IsSingleSteamMode)
            {
                if (BayA.IsInstalled)
                {
                    BayB.SetDirectory(BayA.DirectoryPath);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(BayB.DirectoryPath) && discovery.Count > 1)
                {
                    BayB.SetDirectory(discovery[1].ExePath);
                }
            }

            if (string.IsNullOrWhiteSpace(BayA.DirectoryPath))
            {
                BayA.SetDirectory(DefaultTarget("A"));
            }
            if (!IsSingleSteamMode && string.IsNullOrWhiteSpace(BayB.DirectoryPath))
            {
                BayB.SetDirectory(DefaultTarget("B"));
            }

            if (string.IsNullOrWhiteSpace(BayA.Name))
            {
                BayA.Name = SuggestName(BayA, BayB);
            }
            if (string.IsNullOrWhiteSpace(BayB.Name))
            {
                BayB.Name = SuggestName(BayB, BayA);
            }

            CreateDesktop = _preferences.CreateDesktop;
            CreateStartMenu = _preferences.CreateStartMenu;
            ShortcutLayout = _preferences.ShortcutLayout;
            BayA.ShortcutMode = _preferences.ShortcutA;
            BayB.ShortcutMode = _preferences.ShortcutB;
            ConfirmSwitch = _backend.ConfirmSwitch;
            ShutdownTimeoutSeconds = _backend.ShutdownTimeoutSeconds;
            ThemeMode = _preferences.Theme;
            LanguageSetting = _preferences.Language;
            _managedShortcutCount = null;

            RefreshAccountsInternal(profileA?.Login, profileB?.Login);
            UpdateSummaries();
            ApplyCompleted = HasSavedConfiguration && ShortcutsReady;
            InitialStepIndex = CalculateInitialStep();
            StatusMessage = InitialStepIndex == 3
                ? LocalizationService.T("StatusReady")
                : LocalizationService.T("StatusInitial");
        }
        finally
        {
            _suppressDirty = false;
            IsDirty = false;
        }
    }

    [RelayCommand]
    private void RefreshAccounts()
    {
        RefreshAccountsInternal(
            BayA.SelectedAccount?.AccountName,
            BayB.SelectedAccount?.AccountName);
        UpdateSummaries();
        StatusMessage = LocalizationService.T("StatusAccountsUpdated");
    }

    [RelayCommand]
    private void RefreshDetection()
    {
        var discovery = _discovery.Discover([BayA.ExePath, BayB.ExePath]);
        var unused = discovery
            .Where(install => !install.ExePath.Equals(BayA.ExePath, StringComparison.OrdinalIgnoreCase) &&
                              !install.ExePath.Equals(BayB.ExePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!BayA.IsInstalled && unused.Count > 0)
        {
            BayA.SetDirectory(unused[0].ExePath);
            unused.RemoveAt(0);
        }

        if (!BayB.IsInstalled && unused.Count > 0)
        {
            BayB.SetDirectory(unused[0].ExePath);
        }

        RefreshAccountsInternal(BayA.SelectedAccount?.AccountName, BayB.SelectedAccount?.AccountName);
        IsDirty = true;
        StatusMessage = LocalizationService.T("StatusDetectionUpdated");
        UpdateSummaries();
    }

    public event Action<BayViewModel?>? InstallWizardRequested;

    [RelayCommand]
    private void Install(BayViewModel? bay)
    {
        InstallWizardRequested?.Invoke(bay);
    }

    public void ApplyInstalledBays(string? pathA, string? pathB)
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(pathA) && File.Exists(Path.Combine(pathA, "steam.exe")))
        {
            BayA.SetDirectory(pathA);
            if (string.IsNullOrWhiteSpace(BayA.Name) || IsGeneratedName(BayA.Name))
            {
                BayA.Name = SuggestName(BayA, BayB);
            }

            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(pathB) && File.Exists(Path.Combine(pathB, "steam.exe")))
        {
            BayB.SetDirectory(pathB);
            if (string.IsNullOrWhiteSpace(BayB.Name) || IsGeneratedName(BayB.Name))
            {
                BayB.Name = SuggestName(BayB, BayA);
            }

            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RefreshAccountsInternal(BayA.SelectedAccount?.AccountName, BayB.SelectedAccount?.AccountName);
        IsDirty = true;
        StatusMessage = LocalizationService.T("InstallWizardDoneDesc");
        UpdateSummaries();
    }

    [RelayCommand]
    private void OpenShortcutsFolder()
    {
        var folder = ShortcutService.GetActiveRoot(ShortcutLayout, CreateDesktop, CreateStartMenu);
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = LocalizationService.T("StatusNoAccessFolder");
            return;
        }

        Directory.CreateDirectory(folder);
        OpenDirectory(folder);
    }

    [RelayCommand]
    private void BrowseFolder(BayViewModel? bay)
    {
        if (bay is null)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("StatusBrowseTitle", bay.SlotLabel),
            InitialDirectory = Directory.Exists(bay.DirectoryPath) ? bay.DirectoryPath : string.Empty
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        bay.SetDirectory(dialog.FolderName);
        if (string.IsNullOrWhiteSpace(bay.Name))
        {
            bay.Name = SuggestName(bay, bay == BayA ? BayB : BayA);
        }

        RefreshBayAccounts(bay);
        IsDirty = true;
        StatusMessage = $"{bay.SlotLabel}: {bay.PathDisplay}";
        UpdateSummaries();
    }

    [RelayCommand]
    private async Task OpenSteam(BayViewModel? bay)
    {
        if (bay is null || !bay.IsInstalled)
        {
            StatusMessage = LocalizationService.T("StatusSelectValidSteam");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        var targetAlreadyRunning = _launchService.IsSteamRunning(bay.ExePath);
        if (!targetAlreadyRunning && ConfirmSwitch && _launchService.IsOtherSteamRunning(bay.ExePath))
        {
            var answer = MessageBox.Show(
                LocalizationService.T("ConfirmCloseOtherSteam"),
                LocalizationService.T("DialogTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                StatusMessage = LocalizationService.T("StatusSwitchCancelled");
                return;
            }
        }

        IsBusy = true;
        try
        {
            StatusMessage = targetAlreadyRunning
                ? LocalizationService.T("StatusActivatingSteam", bay.SlotLabel)
                : LocalizationService.T("StatusClosingSteam", bay.SlotLabel);
            var result = await _launchService.RestartAsync(
                bay.ExePath,
                bay.SelectedAccount?.AccountName,
                ShutdownTimeoutSeconds);

            StatusMessage = result.Success
                ? $"{bay.SlotLabel}: {result.Message}"
                : $"{bay.SlotLabel}: {result.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{bay.SlotLabel}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSteamFolder(BayViewModel? bay)
    {
        if (bay is null || !Directory.Exists(bay.DirectoryPath))
        {
            return;
        }

        _accountService.OpenFolder(new SteamInstallationInfo(bay.ExePath));
    }

    [RelayCommand]
    private void ApplyToBoth(BayViewModel? bay)
    {
        var source = bay ?? BayA;
        var target = ReferenceEquals(source, BayA) ? BayB : BayA;
        target.ShortcutMode = source.ShortcutMode;
        IsDirty = true;
        StatusMessage = LocalizationService.T("StatusModeApplied", source.SlotLabel, target.SlotLabel);
        UpdateSummaries();
    }

    [RelayCommand]
    private void SwapBays()
    {
        _suppressDirty = true;
        try
        {
            var directoryA = BayA.DirectoryPath;
            var nameA = BayA.Name;
            var keyA = BayA.ProfileKey;
            var accountA = BayA.SelectedAccount;
            var modeA = BayA.ShortcutMode;

            if (!IsSingleSteamMode)
            {
                BayA.DirectoryPath = BayB.DirectoryPath;
                BayB.DirectoryPath = directoryA;
            }

            BayA.Name = BayB.Name;
            BayA.ProfileKey = BayB.ProfileKey;
            BayA.SelectedAccount = BayB.SelectedAccount;
            BayA.ShortcutMode = BayB.ShortcutMode;

            BayB.Name = nameA;
            BayB.ProfileKey = keyA;
            BayB.SelectedAccount = accountA;
            BayB.ShortcutMode = modeA;

            RefreshBayAccounts(BayA);
            RefreshBayAccounts(BayB);
        }
        finally
        {
            _suppressDirty = false;
        }

        IsDirty = true;
        StatusMessage = LocalizationService.T("StatusSwapped");
        UpdateSummaries();
    }

    public bool ApplyChanges(out int requiredStep)
    {
        requiredStep = -1;
        if (IsBusy)
        {
            return false;
        }

        if (!Validate(out requiredStep, out var validationMessage))
        {
            StatusMessage = validationMessage;
            return false;
        }

        try
        {
            _backend.CreateBackup();
            var profiles = new[]
            {
                ToBackendProfile(BayA),
                ToBackendProfile(BayB)
            };

            _backend.ConfirmSwitch = ConfirmSwitch;
            _backend.ShutdownTimeoutSeconds = Math.Clamp(ShutdownTimeoutSeconds, 5, 120);
            _backend.Save(profiles);
            _backend.Profiles.Clear();
            _backend.Profiles.AddRange(profiles);

            _preferences = new SetupPreferences
            {
                ShortcutA = BayA.ShortcutMode,
                ShortcutB = BayB.ShortcutMode,
                ProfileAKey = BayA.ProfileKey,
                ProfileBKey = BayB.ProfileKey,
                Theme = ThemeMode,
                Language = LanguageSetting,
                ShortcutLayout = ShortcutLayout,
                CreateDesktop = CreateDesktop,
                CreateStartMenu = CreateStartMenu,
                WelcomeCompleted = _preferences.WelcomeCompleted,
                IsSingleSteamMode = IsSingleSteamMode
            };
            _preferences.Save(_context);

            var result = _shortcutService.Sync(
                _context.BackendPath,
                _context.RootDirectory,
                [
                    new ShortcutSpec(
                        BayA.SelectedAccount?.AccountName ?? BayA.ProfileKey,
                        BayA.NameDisplay,
                        BayA.ShortcutMode,
                        BayA.ExePath,
                        IsSingleSteamMode ? null : BayA.DirectoryPath),
                    new ShortcutSpec(
                        BayB.SelectedAccount?.AccountName ?? BayB.ProfileKey,
                        BayB.NameDisplay,
                        BayB.ShortcutMode,
                        BayB.ExePath,
                        IsSingleSteamMode ? null : BayB.DirectoryPath)
                ],
                ShortcutLayout,
                CreateDesktop,
                CreateStartMenu);

            var setupExe = Environment.ProcessPath;
            if (CreateStartMenu && !string.IsNullOrWhiteSpace(setupExe) && File.Exists(setupExe))
            {
                _shortcutService.CreateSetupShortcut(setupExe);
            }

            _managedShortcutCount = null;
            IsDirty = false;
            ApplyCompleted = true;
            UpdateSummaries();
            if (result.Error is not null)
            {
                ApplyCompleted = false;
                StatusMessage = LocalizationService.T("StatusSavedShortcutsFailed", result.Error);
                requiredStep = 2;
                return false;
            }

            StatusMessage = BothAccountsRemembered
                ? LocalizationService.T("StatusSaved")
                : LocalizationService.T("StatusSavedRemember");
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.T("StatusSavedFailed", ex.Message);
            return false;
        }
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
        {
            ApplyCompleted = false;
            IsDirty = true;
        }
    }

    public void RefreshAfterNavigation()
    {
        if (IsBusy)
        {
            return;
        }

        RefreshAccountsInternal(BayA.SelectedAccount?.AccountName, BayB.SelectedAccount?.AccountName);
        UpdateSummaries();
    }

    [RelayCommand]
    private void OpenBackendFolder()
    {
        OpenDirectory(_context.RootDirectory);
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var logs = Path.Combine(_context.RootDirectory, "logs");
        Directory.CreateDirectory(logs);
        OpenDirectory(logs);
    }

    partial void OnApplyCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(SummaryStatus));
    }

    partial void OnCreateDesktopChanged(bool value)
    {
        MarkDirty();
        UpdateSummaries();
    }

    partial void OnCreateStartMenuChanged(bool value)
    {
        MarkDirty();
        UpdateSummaries();
    }

    partial void OnConfirmSwitchChanged(bool value) => MarkDirty();
    partial void OnShutdownTimeoutSecondsChanged(int value) => MarkDirty();

    partial void OnShortcutLayoutChanged(ShortcutLayoutMode value)
    {
        MarkDirty();
        UpdateSummaries();
    }

    partial void OnLanguageSettingChanged(string value)
    {
        LocalizationService.Instance.Apply(value);
        MarkDirty();
    }

    partial void OnThemeModeChanged(SetupThemeMode value)
    {
        ThemeManager.Apply(value);
        MarkDirty();
    }

    private void Bay_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressDirty)
        {
            return;
        }

        if (e.PropertyName is nameof(BayViewModel.Name) or nameof(BayViewModel.DirectoryPath) or
            nameof(BayViewModel.SelectedAccount) or nameof(BayViewModel.ShortcutMode))
        {
            ApplyCompleted = false;
            IsDirty = true;
            UpdateSummaries();
        }
    }

    private void RefreshAccountsInternal(string? preferredA = null, string? preferredB = null)
    {
        var beforeA = BayA.SelectedAccount?.AccountName;
        var beforeB = BayB.SelectedAccount?.AccountName;
        _suppressDirty = true;
        try
        {
            RefreshBayAccounts(BayA, preferredA);
            RefreshBayAccounts(BayB, preferredB);
            RefreshAllAccounts();

            if (IsSingleSteamMode && BayA.Accounts.Count > 1)
            {
                if (BayA.SelectedAccount is null)
                {
                    BayA.SelectedAccount = BayA.Accounts[0];
                }
                if (BayB.SelectedAccount is null || BayB.SelectedAccount.AccountName.Equals(BayA.SelectedAccount?.AccountName, StringComparison.OrdinalIgnoreCase))
                {
                    var secondAccount = BayB.Accounts.FirstOrDefault(a => !a.AccountName.Equals(BayA.SelectedAccount?.AccountName, StringComparison.OrdinalIgnoreCase));
                    if (secondAccount is not null)
                    {
                        BayB.SelectedAccount = secondAccount;
                    }
                }
            }
        }
        finally
        {
            _suppressDirty = false;
        }

        if (!string.Equals(beforeA, BayA.SelectedAccount?.AccountName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(beforeB, BayB.SelectedAccount?.AccountName, StringComparison.OrdinalIgnoreCase))
        {
            MarkDirty();
        }

        UpdateSummaries();
    }

    private void RefreshBayAccounts(BayViewModel bay, string? preferredLogin = null)
    {
        if (!bay.IsInstalled)
        {
            bay.SetAccounts([], preferredLogin);
            return;
        }

        var previous = bay.SelectedAccount?.AccountName;
        var accounts = _discovery.GetAccounts(bay.ExePath);
        bay.SetAccounts(accounts, preferredLogin ?? previous);
    }

    private void UpdateSummaries()
    {
        BayA.UpdateStatus();
        BayB.UpdateStatus();
        OnPropertyChanged(nameof(BackendStatus));
        OnPropertyChanged(nameof(ShortcutSummary));
        OnPropertyChanged(nameof(ShortcutLayoutDescription));
        OnPropertyChanged(nameof(ShortcutLocationsPreview));
        OnPropertyChanged(nameof(InstallationsStatus));
        OnPropertyChanged(nameof(AccountsStatus));
        OnPropertyChanged(nameof(ShortcutsStatus));
        OnPropertyChanged(nameof(SummaryStatus));
        OnPropertyChanged(nameof(BothAccountsRemembered));
        OnPropertyChanged(nameof(HasSavedConfiguration));
        OnPropertyChanged(nameof(NeedsInstallation));
        OnPropertyChanged(nameof(ShortcutsReady));
        OnPropertyChanged(nameof(IsFullyConfigured));
        OnPropertyChanged(nameof(ExpectedShortcutCount));
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(PairStatusTitle));
        OnPropertyChanged(nameof(PairStatusDetail));
    }

    private int CalculateInitialStep()
    {
        if (!HasSavedConfiguration)
        {
            return 0;
        }

        if (IsSingleSteamMode ? !BayA.IsInstalled : (!BayA.IsInstalled || !BayB.IsInstalled))
        {
            return 0;
        }

        if (BayA.SelectedAccount is null || BayB.SelectedAccount is null || SameAccount() ||
            !BothAccountsRemembered)
        {
            return 1;
        }

        if (!ShortcutsReady)
        {
            return 2;
        }

        return 3;
    }

    private bool Validate(out int requiredStep, out string message)
    {
        requiredStep = -1;
        if (!ValidateInstallations(out message))
        {
            requiredStep = 0;
            return false;
        }

        if (!ValidateAccounts(out message))
        {
            requiredStep = 1;
            return false;
        }

        if (!ValidateAccess(out message))
        {
            requiredStep = 2;
            return false;
        }

        if (!_context.HasBackend)
        {
            requiredStep = 3;
            message = LocalizationService.T("ValidationNoBackend");
            return false;
        }

        if (ShutdownTimeoutSeconds is < 5 or > 120)
        {
            requiredStep = 3;
            message = LocalizationService.T("ValidationTimeout");
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool SameDirectory()
    {
        return !string.IsNullOrWhiteSpace(BayA.DirectoryPath) &&
               BayA.DirectoryPath.Equals(BayB.DirectoryPath, StringComparison.OrdinalIgnoreCase);
    }

    private bool AreNestedDirectories()
    {
        var pathA = BayA.DirectoryPath.TrimEnd('\\');
        var pathB = BayB.DirectoryPath.TrimEnd('\\');
        if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB))
        {
            return false;
        }

        return pathA.StartsWith(pathB + "\\", StringComparison.OrdinalIgnoreCase) ||
               pathB.StartsWith(pathA + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private bool SameAccount()
    {
        return BayA.SelectedAccount is not null && BayB.SelectedAccount is not null &&
               BayA.SelectedAccount.AccountName.Equals(BayB.SelectedAccount.AccountName, StringComparison.OrdinalIgnoreCase);
    }

    private BackendProfile ToBackendProfile(BayViewModel bay)
    {
        return new BackendProfile
        {
            Key = string.IsNullOrWhiteSpace(bay.ProfileKey) ? (bay == BayA ? "steam1" : "steam2") : bay.ProfileKey.Trim(),
            Name = string.IsNullOrWhiteSpace(bay.Name) ? bay.SlotLabel : bay.Name.Trim(),
            ExePath = bay.ExePath,
            Login = bay.SelectedAccount?.AccountName ?? string.Empty
        };
    }

    private static string SuggestName(BayViewModel bay, BayViewModel other)
    {
        if (bay.IsSingleSteamMode)
        {
            return bay.Slot == "A" ? "Steam (Main)" : "Steam (Alt)";
        }

        var folderName = Path.GetFileName(bay.DirectoryPath.TrimEnd('\\'));
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            return folderName;
        }

        return bay.SlotLabel;
    }

    private static bool IsGeneratedName(string name)
    {
        return name.Equals("Steam", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Steam OG", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Steam A", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Steam B", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Steam_A", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Steam_B", StringComparison.OrdinalIgnoreCase);
    }

    private BackendProfile? ResolveProfile(string key, int index)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var match = _backend.Profiles.FirstOrDefault(profile =>
                profile.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return _backend.Profiles.ElementAtOrDefault(index);
    }

    private string DefaultTarget(string slot)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        var folder = slot == "A" ? "Steam_A" : "Steam_B";
        var preferred = Path.Combine(root, folder);
        var other = slot == "A" ? BayB.DirectoryPath : BayA.DirectoryPath;
        if (preferred.Equals(other, StringComparison.OrdinalIgnoreCase))
        {
            preferred = Path.Combine(root, folder + "_2");
        }

        return preferred;
    }

    private static int CountMode(ShortcutMode mode) => mode switch
    {
        ShortcutMode.Both => 2,
        ShortcutMode.None => 0,
        _ => 1
    };

    private static void OpenDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }
    public void RefreshAllAccounts()
    {
        AllDiscoveredAccounts.Clear();
        var primary = _coreDiscovery.GetPrimaryInstallation();
        if (primary != null)
        {
            var accounts = _coreAccountService.GetAccounts(primary);
            foreach (var acc in accounts)
            {
                AllDiscoveredAccounts.Add(acc);
            }
        }
    }

    [RelayCommand]
    private async Task QuickLaunchAccount(Core.Models.SteamAccount? account)
    {
        if (account == null) return;
        var profile = new Core.Models.SteamProfile
        {
            Id = account.AccountName,
            DisplayName = account.DisplayName,
            AccountName = account.AccountName,
            Mode = Core.Models.ProfileMode.SingleInstall
        };
        StatusMessage = $"Switching to {account.DisplayName}...";
        await _coreSwitch.SwitchAsync(profile);
        StatusMessage = $"Switched to {account.DisplayName}.";
    }

    [RelayCommand]
    private void QuickCreateDesktopShortcut(Core.Models.SteamAccount? account)
    {
        if (account == null) return;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var targetLnk = Path.Combine(desktop, $"Steam - {Sanitize(account.DisplayName)}.lnk");
        var backendExe = _context.BackendPath;
        var primary = _coreDiscovery.GetPrimaryInstallation();
        var iconPath = primary?.ExePath;

        _coreShortcut.CreateShortcut(
            targetLnk,
            backendExe,
            $"--account \"{account.AccountName}\"",
            _context.RootDirectory,
            $"Launch Steam with {account.DisplayName}",
            iconPath,
            0);

        StatusMessage = $"Shortcut created on Desktop: {Path.GetFileName(targetLnk)}";
    }

    [RelayCommand]
    private void QuickCreateBigPictureShortcut(Core.Models.SteamAccount? account)
    {
        if (account == null) return;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var targetLnk = Path.Combine(desktop, $"Steam - {Sanitize(account.DisplayName)} (Big Picture).lnk");
        var backendExe = _context.BackendPath;
        var primary = _coreDiscovery.GetPrimaryInstallation();
        var iconPath = primary?.ExePath;

        _coreShortcut.CreateShortcut(
            targetLnk,
            backendExe,
            $"--account \"{account.AccountName}\" --bigpicture",
            _context.RootDirectory,
            $"Launch Steam Big Picture with {account.DisplayName}",
            iconPath,
            0);

        StatusMessage = $"Big Picture shortcut created on Desktop: {Path.GetFileName(targetLnk)}";
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(value) ? "Steam" : value;
    }

    public string MultiInstallNotice => LocalizationService.T("MultiInstallNotice");
}
