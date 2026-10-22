using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SteamSwitcher.Setup.Views;

public partial class WelcomeView : UserControl
{
    private static readonly QuadraticEase EaseOut = CreateEaseOut();
    private readonly List<Action> _finalizers = [];
    private Storyboard? _activeStoryboard;
    private EventHandler? _activeCompletedHandler;
    private bool _introPlayed;
    private bool _closing;
    private bool _transitioning;
    private bool _finishing;

    public WelcomeView()
    {
        InitializeComponent();
    }

    public event EventHandler? Finished;

    public void CancelAnimations()
    {
        _closing = true;
        StopActiveStoryboard(finalize: false);
        Finished = null;
    }

    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    private void Welcome_Loaded(object sender, RoutedEventArgs e)
    {
        if (_introPlayed)
        {
            return;
        }

        _introPlayed = true;
        _closing = false;

        if (!AnimationsEnabled)
        {
            SetSceneOneFinal();
            FocusContinueButton();
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_closing || !IsLoaded)
            {
                return;
            }

            FocusContinueButton();
            PlaySceneOneIntro();
        });
    }

    private void Welcome_Unloaded(object sender, RoutedEventArgs e)
    {
        _closing = true;
        StopActiveStoryboard(finalize: false);
        Finished = null;
    }

    private void Welcome_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            FinishWelcome();
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_transitioning || _closing || _finishing)
        {
            return;
        }

        StopActiveStoryboard(finalize: true);

        if (!AnimationsEnabled)
        {
            ShowSceneTwo(animated: false);
            return;
        }

        _transitioning = true;
        var storyboard = new Storyboard();
        AddFade(storyboard, SceneOne, 1, 0, 0, 140);
        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
            ShowSceneTwo(animated: true);
        });
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_closing || _finishing)
        {
            return;
        }

        if (_transitioning)
        {
            StopActiveStoryboard(finalize: true);
            _transitioning = false;
        }

        StopActiveStoryboard(finalize: true);

        if (!AnimationsEnabled)
        {
            ShowSceneOne(animated: false);
            return;
        }

        _transitioning = true;
        var storyboard = new Storyboard();
        AddFade(storyboard, SceneTwo, 1, 0, 0, 140);
        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
            ShowSceneOne(animated: true);
        });
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        FinishWelcome();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        FinishWelcome();
    }

    private void FinishWelcome()
    {
        if (_closing || _finishing)
        {
            return;
        }

        if (_transitioning)
        {
            StopActiveStoryboard(finalize: true);
            _transitioning = false;
        }

        StopActiveStoryboard(finalize: true);
        _finishing = true;

        if (!AnimationsEnabled)
        {
            RaiseFinished();
            return;
        }

        _transitioning = true;
        var storyboard = new Storyboard();
        AddFade(storyboard, this, 1, 0, 0, 180);
        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
            RaiseFinished();
        });
    }

    private void RaiseFinished()
    {
        if (_closing)
        {
            return;
        }

        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void ShowSceneTwo(bool animated)
    {
        SceneOne.Visibility = Visibility.Collapsed;
        SceneOne.Opacity = 1;
        SceneTwo.Visibility = Visibility.Visible;
        SceneTwo.Opacity = 1;
        SceneOneFooter.Visibility = Visibility.Collapsed;
        SceneTwoFooter.Visibility = Visibility.Visible;
        SkipButton.Visibility = Visibility.Collapsed;
        ProgressText.Text = "02 / 02";
        WelcomeScroll.ScrollToTop();
        SetSceneTwoFinal();

        if (!animated || !AnimationsEnabled)
        {
            _transitioning = false;
            FocusStartButton();
            return;
        }

        FocusStartButton();
        PlaySceneTwoIntro();
    }

    private void ShowSceneOne(bool animated)
    {
        SceneTwo.Visibility = Visibility.Collapsed;
        SceneTwo.Opacity = 1;
        SceneOne.Visibility = Visibility.Visible;
        SceneOne.Opacity = 1;
        SceneTwoFooter.Visibility = Visibility.Collapsed;
        SceneOneFooter.Visibility = Visibility.Visible;
        SkipButton.Visibility = Visibility.Visible;
        ProgressText.Text = "01 / 02";
        WelcomeScroll.ScrollToTop();
        SetSceneOneFinal();

        if (!animated || !AnimationsEnabled)
        {
            _transitioning = false;
            FocusContinueButton();
            return;
        }

        FocusContinueButton();
        SceneOne.Opacity = 0;
        var storyboard = new Storyboard();
        AddFade(storyboard, SceneOne, 0, 1, 0, 220);
        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
            _transitioning = false;
        });
    }

    private void PlaySceneOneIntro()
    {
        StopActiveStoryboard(finalize: true);
        SetSceneOneInitial();
        var storyboard = new Storyboard();

        AddFade(storyboard, HeroTitleText, 0, 1, 30, 360);
        AddFade(storyboard, HeroSubtitleText, 0, 1, 100, 330);
        AddFade(storyboard, HeroVisual, 0, 1, 160, 380);
        AddFade(storyboard, BayACard, 0, 1, 150, 340);
        AddFade(storyboard, BayBCard, 0, 1, 210, 340);
        AddFade(storyboard, AfterSetupNote, 0, 1, 260, 320);
        AddFade(storyboard, ConnectorDot, 0, 1, 290, 240);

        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
        });
    }

    private void PlaySceneTwoIntro()
    {
        StopActiveStoryboard(finalize: true);
        SetSceneTwoInitial();
        var storyboard = new Storyboard();

        AddFade(storyboard, SceneTwoTitleText, 0, 1, 0, 300);
        AddFade(storyboard, SceneTwoSubtitleText, 0, 1, 70, 270);
        AddFade(storyboard, DoesCard, 0, 1, 130, 330);
        AddFade(storyboard, DoesNotCard, 0, 1, 200, 330);
        AddFade(storyboard, NextStepsStrip, 0, 1, 300, 300);

        StartStoryboard(storyboard, () =>
        {
            CompleteAnimations();
            _transitioning = false;
        });
    }

    private void SetSceneOneInitial()
    {
        SetOpacity(HeroTitleText, 0);
        SetOpacity(HeroSubtitleText, 0);
        SetOpacity(HeroVisual, 0);
        SetOpacity(BayACard, 0);
        SetOpacity(BayBCard, 0);
        SetOpacity(AfterSetupNote, 0);
        SetOpacity(ConnectorDot, 0);
    }

    private void SetSceneOneFinal()
    {
        SetOpacity(HeroTitleText, 1);
        SetOpacity(HeroSubtitleText, 1);
        SetOpacity(HeroVisual, 1);
        SetOpacity(BayACard, 1);
        SetOpacity(BayBCard, 1);
        SetOpacity(AfterSetupNote, 1);
        SetOpacity(ConnectorDot, 1);
    }

    private void SetSceneTwoInitial()
    {
        SetOpacity(SceneTwoTitleText, 0);
        SetOpacity(SceneTwoSubtitleText, 0);
        SetOpacity(DoesCard, 0);
        SetOpacity(DoesNotCard, 0);
        SetOpacity(NextStepsStrip, 0);
    }

    private void SetSceneTwoFinal()
    {
        SetOpacity(SceneTwoTitleText, 1);
        SetOpacity(SceneTwoSubtitleText, 1);
        SetOpacity(DoesCard, 1);
        SetOpacity(DoesNotCard, 1);
        SetOpacity(NextStepsStrip, 1);
    }

    private static void SetOpacity(UIElement element, double value)
    {
        element.Opacity = value;
    }

    private void StartStoryboard(Storyboard storyboard, Action? completed)
    {
        _activeStoryboard = storyboard;

        if (completed is not null)
        {
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                storyboard.Completed -= handler;
                _activeCompletedHandler = null;
                if (_closing)
                {
                    return;
                }

                _activeStoryboard = null;
                CompleteAnimations();
                completed();
            };
            _activeCompletedHandler = handler;
            storyboard.Completed += handler;
        }

        try
        {
            storyboard.Begin(this, true);
        }
        catch
        {
            _activeStoryboard = null;
            _activeCompletedHandler = null;
            CompleteAnimations();
            completed?.Invoke();
        }
    }

    private void StopActiveStoryboard(bool finalize)
    {
        if (_activeStoryboard is null)
        {
            _activeCompletedHandler = null;
            if (finalize)
            {
                CompleteAnimations();
            }
            else
            {
                _finalizers.Clear();
            }

            return;
        }

        if (_activeCompletedHandler is not null)
        {
            _activeStoryboard.Completed -= _activeCompletedHandler;
            _activeCompletedHandler = null;
        }

        _activeStoryboard.Stop(this);
        _activeStoryboard = null;

        if (finalize)
        {
            CompleteAnimations();
        }
        else
        {
            _finalizers.Clear();
        }
    }

    private void CompleteAnimations()
    {
        foreach (var finalizer in _finalizers)
        {
            finalizer();
        }

        _finalizers.Clear();
        _activeStoryboard = null;
    }

    private void FocusContinueButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_closing && IsVisible)
            {
                ContinueButton.Focus();
            }
        });
    }

    private void FocusStartButton()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_closing && IsVisible)
            {
                StartButton.Focus();
            }
        });
    }

    private void AddFade(
        Storyboard storyboard,
        UIElement target,
        double from,
        double to,
        double beginMilliseconds,
        double durationMilliseconds)
    {
        var animation = new DoubleAnimation(from, to, Duration(durationMilliseconds))
        {
            BeginTime = TimeSpan.FromMilliseconds(beginMilliseconds),
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.HoldEnd
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(animation);
        _finalizers.Add(() =>
        {
            target.Opacity = to;
            target.BeginAnimation(UIElement.OpacityProperty, null);
        });
    }

    private static Duration Duration(double milliseconds) =>
        new(TimeSpan.FromMilliseconds(milliseconds));

    private static QuadraticEase CreateEaseOut()
    {
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        easing.Freeze();
        return easing;
    }
}
