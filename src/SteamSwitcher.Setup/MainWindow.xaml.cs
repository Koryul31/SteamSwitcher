using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SteamSwitcher.Setup.Localization;
using SteamSwitcher.Setup.Services;
using SteamSwitcher.Setup.ViewModels;
using SteamSwitcher.Setup.Views;

namespace SteamSwitcher.Setup;

public partial class MainWindow : Window
{
    private readonly SetupViewModel _viewModel;
    private readonly FrameworkElement[] _views;
    private readonly OptionsView _optionsView;
    private int _currentStep;
    private bool _loaded;
    private string? _statusOverride;
    private DateTime _statusUntil;
    private readonly DispatcherTimer _statusTimer;
    private WelcomeView? _welcomeView;
    private bool _welcomeVisible;
    private bool _welcomeReplay;
    private InstallWizardView? _installView;
    private InstallWizardViewModel? _installViewModel;
    private bool _installVisible;
    private bool _optionsVisible;

    private static readonly string[] Kickers =
    [
        "PageInstallKicker",
        "PageAccountsKicker",
        "PageAccessKicker",
        "PageSummaryKicker"
    ];

    private static readonly string[] Titles =
    [
        "StepInstallations",
        "StepAccounts",
        "StepAccess",
        "StepSummary"
    ];

    private static readonly string[] Subtitles =
    [
        "PageInstallSubtitle",
        "PageAccountsSubtitle",
        "PageAccessSubtitle",
        "PageSummarySubtitle"
    ];

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new SetupViewModel();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.InstallWizardRequested += SetupViewModel_InstallWizardRequested;
        _optionsView = new OptionsView();
        _optionsView.WelcomeRequested += OptionsView_WelcomeRequested;
        _optionsView.DataContext = _viewModel;
        _views =
        [
            new InstallationsView(),
            new AccountsView(),
            new AccessView(),
            new SummaryView()
        ];

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _statusOverride = null;
            UpdateVisualState();
        };

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        DpiChanged += MainWindow_DpiChanged;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        };
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Activated += MainWindow_Activated;
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        LocalizationService.LanguageChanged += Localization_LanguageChanged;
        Journey.StepSelected += Journey_StepSelected;
        AddHandler(UIElement.MouseEnterEvent, new MouseEventHandler(OnMouseEnter), true);
        AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnMouseLeave), true);
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnSelectionChanged), true);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowLayoutService.Fit(this);
        WindowLayoutService.SetCornerPreference(this);
        ApplyFlowDirection();
        _loaded = true;

        if (_viewModel.ShouldShowWelcome)
        {
            ShowWelcome(replay: false);
        }
        else
        {
            ShowStep(_viewModel.InitialStepIndex, animate: false);
        }

        UpdateVisualState();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (_loaded && !_welcomeVisible && !_installVisible && !_optionsVisible && _currentStep == 1)
        {
            _viewModel.RefreshAfterNavigation();
            UpdateVisualState();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        WindowLayoutService.Fit(this);
        WindowLayoutService.SetCornerPreference(this);
    }

    private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
    {
        WindowLayoutService.Fit(this);
        WindowLayoutService.SetCornerPreference(this);
    }

    private void OptionsView_WelcomeRequested(object? sender, EventArgs e)
    {
        if (_welcomeVisible || _installVisible || !_loaded)
        {
            return;
        }

        HideOptions();
        ShowWelcome(replay: true);
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowOptions();
    }

    private void OptionsCloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideOptions();
    }

    private void ShowOptions()
    {
        if (!_loaded || _optionsVisible || _welcomeVisible || _installVisible)
        {
            return;
        }

        _optionsVisible = true;
        OptionsContent.Content = _optionsView;
        SetupContentGrid.Visibility = Visibility.Collapsed;
        DockBorder.Visibility = Visibility.Collapsed;
        OptionsHost.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            if (_optionsVisible)
            {
                OptionsCloseButton.Focus();
            }
        });
    }

    private void HideOptions()
    {
        if (!_optionsVisible)
        {
            return;
        }

        _optionsVisible = false;
        OptionsHost.Visibility = Visibility.Collapsed;
        OptionsContent.Content = null;
        SetupContentGrid.Visibility = Visibility.Visible;
        DockBorder.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            if (_loaded)
            {
                ContinueButton.Focus();
            }
        });
    }

    private void SetupViewModel_InstallWizardRequested(BayViewModel? bay)
    {
        if (_welcomeVisible || _installVisible || _optionsVisible || !_loaded)
        {
            return;
        }

        ShowInstallWizard(bay);
    }

    private void ShowInstallWizard(BayViewModel? preselectedBay)
    {
        if (_installVisible || _welcomeVisible || _optionsVisible)
        {
            return;
        }

        _installVisible = true;
        SetupContentGrid.Visibility = Visibility.Collapsed;
        DockBorder.Visibility = Visibility.Collapsed;
        InstallHost.Visibility = Visibility.Visible;

        var viewModel = new InstallWizardViewModel(_viewModel, preselectedBay);
        var view = new InstallWizardView { DataContext = viewModel };
        viewModel.Finished += InstallWizard_Finished;
        _installViewModel = viewModel;
        _installView = view;
        InstallHost.Content = view;
    }

    private void InstallWizard_Finished(object? sender, EventArgs e)
    {
        if (!_installVisible)
        {
            return;
        }

        ReleaseInstallWizard();
        var nextStep = _viewModel.BayA.IsInstalled && _viewModel.BayB.IsInstalled ? 1 : 0;
        ShowStep(nextStep, animate: true);
        UpdateVisualState();
        Dispatcher.BeginInvoke(() =>
        {
            if (_loaded)
            {
                ContinueButton.Focus();
            }
        });
    }

    private void ReleaseInstallWizard()
    {
        if (_installViewModel is not null)
        {
            _installViewModel.Finished -= InstallWizard_Finished;
            _installViewModel = null;
        }

        if (_installView is not null)
        {
            _installView.CancelAnimations();
            _installView = null;
        }

        InstallHost.Content = null;
        InstallHost.Visibility = Visibility.Collapsed;
        SetupContentGrid.Visibility = Visibility.Visible;
        DockBorder.Visibility = Visibility.Visible;
        _installVisible = false;
    }

    private void ShowWelcome(bool replay)
    {
        if (_welcomeVisible)
        {
            return;
        }

        _welcomeVisible = true;
        _welcomeReplay = replay;

        SetupContentGrid.Visibility = Visibility.Collapsed;
        DockBorder.Visibility = Visibility.Collapsed;
        WelcomeHost.Visibility = Visibility.Visible;

        var welcome = new WelcomeView();
        welcome.Finished += Welcome_Finished;
        _welcomeView = welcome;
        WelcomeHost.Content = welcome;
    }

    private void Welcome_Finished(object? sender, EventArgs e)
    {
        if (!_welcomeVisible)
        {
            return;
        }

        var replay = _welcomeReplay;
        ReleaseWelcome();

        if (!replay)
        {
            _viewModel.MarkWelcomeCompleted();
        }

        ShowStep(0, animate: true);
        UpdateVisualState();
        Dispatcher.BeginInvoke(() =>
        {
            if (_loaded)
            {
                ContinueButton.Focus();
            }
        });
    }

    private void ReleaseWelcome()
    {
        if (_welcomeView is not null)
        {
            _welcomeView.Finished -= Welcome_Finished;
            _welcomeView.CancelAnimations();
            _welcomeView = null;
        }

        WelcomeHost.Content = null;
        WelcomeHost.Visibility = Visibility.Collapsed;
        SetupContentGrid.Visibility = Visibility.Visible;
        DockBorder.Visibility = Visibility.Visible;
        _welcomeVisible = false;
    }

    private void Journey_StepSelected(int index)
    {
        if (index < 0 || index >= _views.Length || _optionsVisible)
        {
            return;
        }

        if (index <= _currentStep)
        {
            ShowStep(index);
            return;
        }

        if (_viewModel.IsBusy)
        {
            return;
        }

        // Allow direct forward navigation when every previous step is valid,
        // without skipping any required validation.
        if (!_viewModel.CanNavigateTo(index, out var message))
        {
            SetStatusOverride(message);
            UpdateVisualState();
            return;
        }

        ShowStep(index);
    }

    private void UpdateLocalizedHeader()
    {
        if (_currentStep < 0 || _currentStep >= Kickers.Length)
        {
            return;
        }

        StepKickerText.Text = LocalizationService.T(Kickers[_currentStep]);
        PageTitleText.Text = LocalizationService.T(Titles[_currentStep]);
        PageSubtitleText.Text = LocalizationService.T(Subtitles[_currentStep]);
    }

    private void ApplyFlowDirection()
    {
        RootGrid.FlowDirection = LocalizationService.Instance.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        TitleBarBorder.FlowDirection = FlowDirection.LeftToRight;
        Journey.FlowDirection = FlowDirection.LeftToRight;
    }

    private void ShowStep(int index, bool animate = true)
    {
        if (index < 0 || index >= _views.Length)
        {
            return;
        }

        _currentStep = index;
        ViewHost.Content = _views[index];
        UpdateLocalizedHeader();

        if (index == 1)
        {
            _viewModel.RefreshAfterNavigation();
        }

        UpdateVisualState();

        if (!animate || !_loaded || !SystemParameters.ClientAreaAnimation)
        {
            ViewHost.Opacity = 1;
            ViewTranslate.Y = 0;
            return;
        }

        ViewHost.Opacity = 0;
        ViewTranslate.Y = 6;
        var duration = TimeSpan.FromMilliseconds(180);
        ViewHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        ViewTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void UpdateVisualState()
    {
        var states = new[]
        {
            _viewModel.InstallationsStatus,
            _viewModel.AccountsStatus,
            _viewModel.ShortcutsStatus,
            _viewModel.SummaryStatus
        };
        var completed = new[]
        {
            _viewModel.InstallationsStatus == LocalizationService.T("StateReady"),
            _viewModel.AccountsStatus == LocalizationService.T("StateReady"),
            _viewModel.ShortcutsReady,
            _viewModel.ApplyCompleted || _viewModel.IsFullyConfigured
        };
        Journey.UpdateSteps(_currentStep, states, completed);

        BackButton.IsEnabled = _currentStep > 0;
        ContinueButton.Content = GetPrimaryActionLabel();
        ContinueButton.IsEnabled = !_viewModel.IsBusy;
        ContinueButton.ToolTip = GetPrimaryActionToolTip();

        var isSummaryStep = _currentStep == _views.Length - 1;

        if (_viewModel.IsBusy)
        {
            FooterStatusText.Text = _viewModel.StatusMessage;
            FooterStatusText.Foreground = (Brush)FindResource("AccentBrush");
        }
        else if (_statusOverride is not null && DateTime.Now < _statusUntil)
        {
            FooterStatusText.Text = _statusOverride;
            FooterStatusText.Foreground = (Brush)FindResource("AccentBrush");
        }
        else if (isSummaryStep && !_viewModel.ApplyCompleted)
        {
            FooterStatusText.Text = LocalizationService.T("ChangesPending");
            FooterStatusText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else if (isSummaryStep && _viewModel.ApplyCompleted)
        {
            FooterStatusText.Text = LocalizationService.T("StatusSaved");
            FooterStatusText.Foreground = (Brush)FindResource("AccentBrush");
        }
        else
        {
            FooterStatusText.Text = LocalizationService.T("NoChanges");
            FooterStatusText.Foreground = (Brush)FindResource("MutedBrush");
        }

        FooterStatusText.ToolTip = _viewModel.StatusMessage;
    }

    private string GetPrimaryActionLabel()
    {
        return _currentStep switch
        {
            0 when !_viewModel.BayA.IsInstalled || !_viewModel.BayB.IsInstalled => LocalizationService.T("InstallSteam"),
            0 => LocalizationService.T("ContinueToAccounts"),
            1 when _viewModel.CanContinueFrom(1, out _) => LocalizationService.T("ContinueToAccess"),
            1 when _viewModel.GetPendingAccountBay() is { } pending => LocalizationService.T("OpenSteamToSignIn", pending.Slot),
            2 => LocalizationService.T("ReviewConfiguration"),
            3 when _viewModel.ApplyCompleted => LocalizationService.T("ActionClose"),
            3 => LocalizationService.T("ApplyAndCreate"),
            _ => LocalizationService.T("ActionContinue")
        };
    }

    private string? GetPrimaryActionToolTip()
    {
        if (_currentStep == 0 && (!_viewModel.BayA.IsInstalled || !_viewModel.BayB.IsInstalled))
        {
            return LocalizationService.T("WarningMissingInstall");
        }

        return _viewModel.CanContinueFrom(_currentStep, out var message) ? null : message;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            ShowStep(_currentStep - 1);
        }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        if (_currentStep == 3)
        {
            if (_viewModel.ApplyCompleted)
            {
                Close();
                return;
            }

            if (_viewModel.ApplyChanges(out var requiredStep))
            {
                SetStatusOverride(LocalizationService.T("StatusSaved"));
                UpdateVisualState();
                return;
            }

            if (requiredStep >= 0)
            {
                ShowStep(requiredStep);
            }

            UpdateVisualState();
            return;
        }

        if (!_viewModel.CanContinueFrom(_currentStep, out var message))
        {
            if (_currentStep == 0)
            {
                var missingBay = _viewModel.BayA.IsInstalled ? _viewModel.BayB : _viewModel.BayA;
                ShowInstallWizard(missingBay);
                return;
            }

            if (_currentStep == 1 && _viewModel.GetPendingAccountBay() is { } pendingBay)
            {
                _viewModel.OpenSteamCommand.Execute(pendingBay);
                return;
            }

            SetStatusOverride(message);
            UpdateVisualState();
            return;
        }

        ShowStep(_currentStep + 1);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_installVisible && _installViewModel?.IsBusy == true)
        {
            MessageBox.Show(
                LocalizationService.T("InstallWizardBusyClose"),
                LocalizationService.T("DialogTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            e.Cancel = true;
            return;
        }

        if (!_viewModel.IsDirty)
        {
            return;
        }

        var answer = MessageBox.Show(
            LocalizationService.T("DialogDiscardChanges"),
            LocalizationService.T("DialogTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void ThemeManager_ThemeChanged(SetupThemeMode mode)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateVisualState();
            if (_loaded && SystemParameters.ClientAreaAnimation)
            {
                RootGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(220)));
            }
        });
    }

    private void Localization_LanguageChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyFlowDirection();
            UpdateLocalizedHeader();
            UpdateVisualState();
            if (_loaded && SystemParameters.ClientAreaAnimation)
            {
                RootGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(220)));
            }
        });
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _statusTimer.Stop();
        _welcomeView?.CancelAnimations();
        _welcomeView = null;
        _installViewModel?.Abort();
        _installViewModel = null;
        _installView?.CancelAnimations();
        _installView = null;
        Activated -= MainWindow_Activated;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.InstallWizardRequested -= SetupViewModel_InstallWizardRequested;
        _optionsView.WelcomeRequested -= OptionsView_WelcomeRequested;
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        LocalizationService.LanguageChanged -= Localization_LanguageChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupViewModel.StatusMessage) && !_viewModel.IsDirty)
        {
            SetStatusOverride(_viewModel.StatusMessage);
        }

        if (e.PropertyName is nameof(SetupViewModel.StatusMessage) or
            nameof(SetupViewModel.IsBusy) or
            nameof(SetupViewModel.IsDirty))
        {
            Dispatcher.BeginInvoke(UpdateVisualState);
        }
    }

    private void SetStatusOverride(string? text)
    {
        _statusOverride = text;
        _statusUntil = DateTime.Now.AddSeconds(3);
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private static void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var button = FindAncestor<Button>(source);
        if (button is null || !button.IsEnabled)
        {
            return;
        }

        AnimateButtonLift(button, -1);
    }

    private static void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var button = FindAncestor<Button>(source);
        if (button is null)
        {
            return;
        }

        AnimateButtonLift(button, 0);
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || e.OriginalSource is not ComboBox combo)
        {
            return;
        }

        var animation = new DoubleAnimation(0.68, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        combo.BeginAnimation(OpacityProperty, animation);
    }

    private static void AnimateButtonLift(Button button, double target)
    {
        if (button.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            button.RenderTransform = translate;
        }

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        translate.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
