using System.Windows;
using System.Windows.Controls;

namespace SteamSwitcher.Setup.Controls;

public partial class InfoTip : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(InfoTip),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(object),
        typeof(InfoTip),
        new PropertyMetadata(null));

    public InfoTip()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
