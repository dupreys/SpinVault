using System.Windows;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias.Views;

public partial class AnniversariesWindow : Window
{
    public AnniversariesWindow(string title, string subtitle, IReadOnlyList<AnniversaryRow> rows)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        AlbumList.ItemsSource = rows;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
        MaxHeight = SystemParameters.WorkArea.Height - 20;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
