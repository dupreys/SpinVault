using System.Windows;
using System.Windows.Threading;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;
using ColecaoMidias.Views;

namespace ColecaoMidias;

public partial class App : Application
{
    // Roda antes de o App.xaml carregar os estilos, para que todo texto já nasça no idioma escolhido.
    static App() => Loc.Init(SettingsStore.Current.Language);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        ThemeService.Load();   // precisa vir antes de criar as telas (define as cores)

        var window = new MainWindow { DataContext = new MainViewModel() };
        MainWindow = window;
        window.Show();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, Loc.T("Ocorreu um erro inesperado"),
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
