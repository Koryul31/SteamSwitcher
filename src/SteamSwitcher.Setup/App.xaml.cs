using System.Windows;

namespace SteamSwitcher.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var context = Services.SetupContext.Locate();
        var preferences = Services.SetupPreferences.Load(context);
        Localization.LocalizationService.Instance.Apply(preferences.Language);
        Services.ThemeManager.Apply(preferences.Theme);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
