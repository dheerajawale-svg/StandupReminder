using Microsoft.UI.Xaml;

namespace StandupReminder.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ShellViewModel ViewModel { get; }
}