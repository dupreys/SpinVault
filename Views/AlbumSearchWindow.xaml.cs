using System.Windows;
using System.Windows.Input;
using ColecaoMidias.Services;

namespace ColecaoMidias.Views;

public partial class AlbumSearchWindow : Window
{
    public AlbumMatch? Selected { get; private set; }

    public AlbumSearchWindow(IReadOnlyList<AlbumMatch> matches)
    {
        InitializeComponent();
        DataContext = matches;
        ResultList.SelectedIndex = 0;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
    }

    private void OnUse(object sender, RoutedEventArgs e) => Accept();

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultList.SelectedItem is not null)
            Accept();
    }

    private void Accept()
    {
        if (ResultList.SelectedItem is not AlbumMatch match)
            return;

        Selected = match;
        DialogResult = true;
    }
}
