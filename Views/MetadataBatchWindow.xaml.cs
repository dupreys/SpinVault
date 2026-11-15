using System.Windows;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias.Views;

public partial class MetadataBatchWindow : Window
{
    private readonly MetadataBatchViewModel _viewModel;

    public MetadataBatchWindow(MetadataBatchViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
        Loaded += async (_, _) => await viewModel.RunAsync();   // começa sozinho ao abrir
        Closing += (_, _) => viewModel.Cancel();                // fechar a janela cancela o que falta
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRunning)
            _viewModel.Cancel();    // o botão vira "Fechar" quando o download termina
        else
            Close();
    }
}
