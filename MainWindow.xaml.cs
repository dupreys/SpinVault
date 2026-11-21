using System.Windows;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias;

public partial class MainWindow : Window
{
    /// <summary>Abaixo desta largura o menu lateral mostra só os ícones e as margens diminuem.</summary>
    private const double CompactBelow = 1100;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
        Loaded += (_, _) => (DataContext as MainViewModel)?.StartBackgroundTasks();
        Closing += (_, _) => (DataContext as MainViewModel)?.SyncOnExit();
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width);
        Loaded += (_, _) => ApplyLayout(ActualWidth);
    }

    private void ApplyLayout(double width)
    {
        var compact = width < CompactBelow;
        if (DataContext is MainViewModel viewModel)
            viewModel.IsCompact = compact;

        SidebarColumn.Width = new GridLength(compact ? 86 : 240);
        SidebarPanel.Margin = compact ? new Thickness(10, 22, 10, 16) : new Thickness(18, 24, 18, 20);
        LogoPanel.Margin = compact ? new Thickness(11, 0, 0, 26) : new Thickness(4, 0, 0, 32);
        ContentGrid.Margin = compact ? new Thickness(20, 18, 20, 16) : new Thickness(32, 26, 32, 24);
    }
}
