using System.Windows;
using System.Windows.Controls;

namespace SteamSwitcher.Setup.Views;

public partial class OptionsView : UserControl
{
    public OptionsView()
    {
        InitializeComponent();
    }

    public event EventHandler? WelcomeRequested;

    private void WelcomeButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeRequested?.Invoke(this, EventArgs.Empty);
    }
}
