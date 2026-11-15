using System.Windows;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
        MaxHeight = SystemParameters.WorkArea.Height - 20;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
