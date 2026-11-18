using System.Windows;
using System.Windows.Controls;

namespace ColecaoMidias.Views;

public partial class InsightsView : UserControl
{
    public InsightsView() => InitializeComponent();

    /// <summary>Em janela estreita os cartões empilham (1 coluna); na larga ficam lado a lado.</summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResponsiveLayout.SetColumns(SpendGrid, e.NewSize.Width < 1000 ? 1 : 3, gap: 12);
        ResponsiveLayout.SetColumns(KpiGrid, e.NewSize.Width < 600 ? 1 : 3, gap: 12);
    }

    private void OnShareClick(object sender, RoutedEventArgs e) => SharePopup.IsOpen = true;

    private void OnShareItemClick(object sender, RoutedEventArgs e) => SharePopup.IsOpen = false;
}
