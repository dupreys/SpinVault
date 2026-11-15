using System.Windows;
using ColecaoMidias.Services;

namespace ColecaoMidias;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
    }
}
