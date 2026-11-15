using System.Windows;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias.Views;

public partial class DuplicatesWindow : Window
{
    public DuplicatesWindow(DuplicatesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
    }
}
