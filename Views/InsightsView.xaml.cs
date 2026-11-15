using System.Windows;
using System.Windows.Controls;

namespace ColecaoMidias.Views;

public partial class InsightsView : UserControl
{
    public InsightsView() => InitializeComponent();

    /// <summary>Em janela estreita os cartões empilham (1 coluna); na larga ficam lado a lado.</summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SpendGrid.Columns = e.NewSize.Width < 1100 ? 1 : 3;
        KpiGrid.Columns = e.NewSize.Width < 700 ? 1 : 3;
    }

    private void OnShareClick(object sender, RoutedEventArgs e) => SharePopup.IsOpen = true;

    private void OnShareItemClick(object sender, RoutedEventArgs e) => SharePopup.IsOpen = false;
}
