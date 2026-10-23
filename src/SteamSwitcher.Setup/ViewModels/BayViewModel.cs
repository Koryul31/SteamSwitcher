using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Models;

namespace SteamSwitcher.Setup.ViewModels;

public sealed partial class BayViewModel : ObservableObject
{
    private static readonly Brush WarningStatusBrush =
        new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x6B));
    private static readonly Brush NeutralStatusBrush =
        new SolidColorBrush(Color.FromRgb(0x91, 0xA2, 0xB5));

    private readonly Color _accent;

    [ObservableProperty]
    private string _profileKey = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _directoryPath = string.Empty;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private SteamAccountInfo? _selectedAccount;

    [ObservableProperty]
    private ShortcutMode _shortcutMode = ShortcutMode.Both;

    [ObservableProperty]
    private string _statusText = LocalizationService.T("BayNoConfig");

    [ObservableProperty]
    private string _accountsSummary = LocalizationService.T("AccountsNone");

    [ObservableProperty]
    private bool _isSingleSteamMode = true;

    public string BayTitle => IsSingleSteamMode
        ? (Slot == "A" ? LocalizationService.T("BayPrimarySteam") : LocalizationService.T("BaySecondaryProfile"))
        : LocalizationService.T("BayInstallationSlot", Slot);

    public string AccountSlotTitle => IsSingleSteamMode
        ? LocalizationService.T("BayProfileSlot", Slot)
        : LocalizationService.T("BayInstallationSlot", Slot);

    public string AccountSubheader => IsSingleSteamMode
        ? LocalizationService.T("BaySharedFolder")
        : PathDisplay;

    public BayViewModel(string slot, string accentHex)
    {
        Slot = slot;
        _accent = (Color)ColorConverter.ConvertFromString(accentHex);
        AccentBrush = new SolidColorBrush(_accent);
        AccentSoftBrush = new SolidColorBrush(Color.FromArgb(0x24, _accent.R, _accent.G, _accent.B));
        StatusBrush = NeutralStatusBrush;
    }

    public string Slot { get; }
    public Brush AccentBrush { get; }
    public Brush AccentSoftBrush { get; }
    public Brush StatusBrush { get; private set; }
    public ObservableCollection<SteamAccountInfo> Accounts { get; } = [];

    public string SlotLabel => $"Steam {Slot}";
    public string ExePath => string.IsNullOrWhiteSpace(DirectoryPath) ? string.Empty : Path.Combine(DirectoryPath, "steam.exe");
    public string PathDisplay => string.IsNullOrWhiteSpace(DirectoryPath)
        ? LocalizationService.T("PathNotSelected")
        : DirectoryPath;
    public string NameDisplay => string.IsNullOrWhiteSpace(Name) ? SlotLabel : Name;
    public string InstallationStatusText => IsInstalled
        ? LocalizationService.T("StateReady")
        : LocalizationService.T("BayNotInstalled");
    public bool HasAccounts => Accounts.Count > 0;
    public bool IsAccountReady => SelectedAccount?.RememberPassword == true;
    public string RememberText => SelectedAccount is null
        ? LocalizationService.T("RememberNone")
        : SelectedAccount.RememberPassword
            ? LocalizationService.T("RememberActive")
            : LocalizationService.T("RememberInactive");
    public string OpenSteamActionText => SelectedAccount is null
        ? LocalizationService.T("OpenSteamToSignIn", Slot)
        : LocalizationService.T("OpenSteamSlot", Slot);
    public string AccountHintText
    {
        get
        {
            if (!IsInstalled)
            {
                return LocalizationService.T("BayNotInstalled");
            }

            if (SelectedAccount is null)
            {
                return LocalizationService.T("AccountNotDetected");
            }

            return SelectedAccount.RememberPassword
                ? LocalizationService.T("AccountReadyHint")
                : LocalizationService.T("AccountRememberHint", Slot);
        }
    }
    public string ShortcutModeLabel => ShortcutMode switch
    {
        ShortcutMode.None => LocalizationService.T("ShortcutNone"),
        ShortcutMode.Normal => LocalizationService.T("ShortcutNormal"),
        ShortcutMode.BigPicture => LocalizationService.T("ShortcutBigPicture"),
        _ => LocalizationService.T("ShortcutBoth")
    };

    public void SetDirectory(string path)
    {
        DirectoryPath = NormalizeDirectory(path);
        IsInstalled = !string.IsNullOrWhiteSpace(ExePath) && File.Exists(ExePath);
        UpdateStatus();
    }

    public void SetAccounts(IEnumerable<SteamAccountInfo> accounts, string? preferredLogin = null)
    {
        var currentLogin = SelectedAccount?.AccountName;
        var preferred = !string.IsNullOrWhiteSpace(preferredLogin)
            ? preferredLogin
            : currentLogin;

        var incoming = accounts.ToList();
        var sameAccounts = incoming.Count == Accounts.Count &&
                           incoming.SequenceEqual(Accounts);

        if (!sameAccounts)
        {
            Accounts.Clear();
            foreach (var account in incoming)
            {
                Accounts.Add(account);
            }
        }

        var selected = Accounts.FirstOrDefault(account =>
            !string.IsNullOrWhiteSpace(preferred) &&
            account.AccountName.Equals(preferred, StringComparison.OrdinalIgnoreCase));

        selected ??= Accounts.FirstOrDefault(account => account.MostRecent);
        selected ??= Accounts.FirstOrDefault(account => account.RememberPassword);
        selected ??= Accounts.FirstOrDefault();

        if (!Equals(SelectedAccount, selected))
        {
            SelectedAccount = selected;
        }

        AccountsSummary = Accounts.Count switch
        {
            0 => LocalizationService.T("AccountsNone"),
            1 => LocalizationService.T("AccountsOne"),
            _ => LocalizationService.T("AccountsMany", Accounts.Count)
        };
        UpdateStatus();
    }

    public void RefreshLocalization()
    {
        AccountsSummary = Accounts.Count switch
        {
            0 => LocalizationService.T("AccountsNone"),
            1 => LocalizationService.T("AccountsOne"),
            _ => LocalizationService.T("AccountsMany", Accounts.Count)
        };
        OnPropertyChanged(nameof(RememberText));
        OnPropertyChanged(nameof(OpenSteamActionText));
        OnPropertyChanged(nameof(AccountHintText));
        OnPropertyChanged(nameof(InstallationStatusText));
        OnPropertyChanged(nameof(ShortcutModeLabel));
        OnPropertyChanged(nameof(BayTitle));
        OnPropertyChanged(nameof(AccountSlotTitle));
        OnPropertyChanged(nameof(AccountSubheader));
        OnPropertyChanged(nameof(PathDisplay));
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (!IsInstalled)
        {
            StatusText = LocalizationService.T("BayNotInstalled");
            StatusBrush = WarningStatusBrush;
            OnPropertyChanged(nameof(StatusBrush));
            return;
        }

        if (SelectedAccount is null)
        {
            StatusText = LocalizationService.T("BayNeedLogin");
            StatusBrush = WarningStatusBrush;
            OnPropertyChanged(nameof(StatusBrush));
            return;
        }

        if (!SelectedAccount.RememberPassword)
        {
            StatusText = LocalizationService.T("BayCheckRemember");
            StatusBrush = WarningStatusBrush;
            OnPropertyChanged(nameof(StatusBrush));
            return;
        }

        StatusText = LocalizationService.T("BayReady");
        StatusBrush = AccentBrush;
        OnPropertyChanged(nameof(StatusBrush));
    }

    partial void OnIsSingleSteamModeChanged(bool value)
    {
        OnPropertyChanged(nameof(BayTitle));
        OnPropertyChanged(nameof(AccountSlotTitle));
        OnPropertyChanged(nameof(AccountSubheader));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(NameDisplay));
    }

    partial void OnDirectoryPathChanged(string value)
    {
        IsInstalled = !string.IsNullOrWhiteSpace(ExePath) && File.Exists(ExePath);
        OnPropertyChanged(nameof(ExePath));
        OnPropertyChanged(nameof(PathDisplay));
        OnPropertyChanged(nameof(InstallationStatusText));
        OnPropertyChanged(nameof(AccountHintText));
        OnPropertyChanged(nameof(OpenSteamActionText));
        UpdateStatus();
    }

    partial void OnSelectedAccountChanged(SteamAccountInfo? value)
    {
        OnPropertyChanged(nameof(IsAccountReady));
        OnPropertyChanged(nameof(RememberText));
        OnPropertyChanged(nameof(AccountHintText));
        OnPropertyChanged(nameof(OpenSteamActionText));
        UpdateStatus();
    }

    partial void OnShortcutModeChanged(ShortcutMode value)
    {
        OnPropertyChanged(nameof(ShortcutModeLabel));
    }

    private static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var value = path.Trim().Trim('"');
        if (Path.GetFileName(value).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(value) ?? value;
        }

        return value;
    }
}
