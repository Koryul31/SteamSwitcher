using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SteamSwitcher.Setup.Controls;

public partial class JourneyBar : UserControl
{
    private int _currentIndex;

    public JourneyBar()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            UpdateProgressLine();
            UpdateSelectionBar(animate: false);
        };
    }

    public event Action<int>? StepSelected;

    public void UpdateSteps(int currentIndex, IReadOnlyList<string> states, IReadOnlyList<bool> completed)
    {
        _currentIndex = currentIndex;
        var markers = new[] { Marker0, Marker1, Marker2, Marker3 };
        var halos = new[] { Halo0, Halo1, Halo2, Halo3 };
        var labels = new[] { Label0, Label1, Label2, Label3 };
        var stateTexts = new[] { State0, State1, State2, State3 };

        var ink = (Brush)FindResource("InkBrush");
        var muted = (Brush)FindResource("MutedBrush");
        var accent = (Brush)FindResource("AccentBrush");
        var accentSoft = (Brush)FindResource("AccentSoftBrush");
        var border = (Brush)FindResource("BorderBrush");
        var dock = (Brush)FindResource("DockBrush");

        for (var index = 0; index < markers.Length; index++)
        {
            var isCurrent = index == currentIndex;
            var isComplete = index < completed.Count && completed[index];
            markers[index].Background = isComplete || isCurrent ? accent : dock;
            markers[index].BorderBrush = isComplete || isCurrent ? accent : border;
            halos[index].Background = accentSoft;
            halos[index].BeginAnimation(OpacityProperty, null);
            halos[index].Opacity = isCurrent ? 1 : 0;
            labels[index].Foreground = isCurrent ? ink : muted;
            labels[index].FontWeight = isCurrent ? FontWeights.Bold : FontWeights.SemiBold;
            stateTexts[index].Text = index < states.Count ? states[index] : string.Empty;
            stateTexts[index].Foreground = isCurrent || isComplete ? accent : muted;
        }

        UpdateProgressLine();
        UpdateSelectionBar(animate: true);
        AnimateCurrentMarker(halos, currentIndex);
    }

    private void AnimateCurrentMarker(IReadOnlyList<Border> halos, int currentIndex)
    {
        if (currentIndex < 0 || currentIndex >= halos.Count ||
            !SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var halo = halos[currentIndex];
        var animation = new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        halo.BeginAnimation(OpacityProperty, animation);
    }

    private void UpdateProgressLine()
    {
        if (RootGrid.ActualWidth <= 0)
        {
            return;
        }

        var stepWidth = RootGrid.ActualWidth / 4.0;
        BaseLine.Width = Math.Max(0, stepWidth * 3);
        ProgressLine.Width = Math.Max(0, stepWidth * _currentIndex);
    }

    private void UpdateSelectionBar(bool animate)
    {
        if (RootGrid.ActualWidth <= 0)
        {
            return;
        }

        var stepWidth = RootGrid.ActualWidth / 4.0;
        var targetWidth = Math.Max(24, stepWidth - 16);
        var targetX = stepWidth * _currentIndex;

        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            SelectionBar.Width = targetWidth;
            SelectionBarTranslate.X = targetX;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        SelectionBar.BeginAnimation(WidthProperty, new DoubleAnimation(SelectionBar.Width, targetWidth, duration)
        {
            EasingFunction = ease
        });
        SelectionBarTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(SelectionBarTranslate.X, targetX, duration)
        {
            EasingFunction = ease
        });
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var index))
        {
            StepSelected?.Invoke(index);
        }
    }
}
