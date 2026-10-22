using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SteamSwitcher.Setup.ViewModels;

namespace SteamSwitcher.Setup.Views;

public partial class InstallWizardView : UserControl
{
    private InstallWizardViewModel? _viewModel;
    private bool _closing;

    public InstallWizardView()
    {
        InitializeComponent();
    }

    private void Wizard_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as InstallWizardViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_closing && IsVisible)
            {
                FocusPage();
            }
        });
    }

    private void Wizard_Unloaded(object sender, RoutedEventArgs e)
    {
        _closing = true;
        DetachViewModel();
    }

    public void CancelAnimations()
    {
        _closing = true;
        DetachViewModel();
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel = null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallWizardViewModel.Page))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!_closing && IsVisible)
                {
                    FocusPage();
                }
            });
        }
    }

    private void FocusPage()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.IsDestinations)
        {
            StartButton.Focus();
        }
        else if (_viewModel.IsDone)
        {
            FinishButton.Focus();
        }
    }
}
