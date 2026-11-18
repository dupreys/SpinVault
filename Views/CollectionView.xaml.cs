using System.Windows;
using System.Windows.Controls;

namespace ColecaoMidias.Views;

public partial class CollectionView : UserControl
{
    private bool _headerStacked;

    public CollectionView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Layout responsivo: em janela estreita os botões do topo descem para baixo do título
    /// e o painel de detalhes fica mais fino, para sobrar espaço para as capas.
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        DetailsPanel.Width = ResponsiveLayout.DetailsWidth(width);
        DetailsPanel.Margin = new Thickness(width >= 780 ? 18 : 12, 0, 0, 0);
        DetailsPanel.Padding = new Thickness(width >= 780 ? 18 : 14);
        SearchHost.Width = width < 520 ? Math.Max(160, width - 4) : 250;

        var stacked = width < 880;
        if (stacked == _headerStacked)
            return;
        _headerStacked = stacked;

        if (stacked)
        {
            Grid.SetRow(HeaderActions, 1);
            Grid.SetColumn(HeaderActions, 0);
            Grid.SetColumnSpan(HeaderActions, 2);
            HeaderActions.HorizontalAlignment = HorizontalAlignment.Left;
            HeaderActions.Margin = new Thickness(0, 10, 0, 0);
        }
        else
        {
            Grid.SetRow(HeaderActions, 0);
            Grid.SetColumn(HeaderActions, 1);
            Grid.SetColumnSpan(HeaderActions, 1);
            HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
            HeaderActions.Margin = new Thickness(0);
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = true;

    private void OnOrderClick(object sender, RoutedEventArgs e) => OrderPopup.IsOpen = true;

    private void OnToolsClick(object sender, RoutedEventArgs e) => ToolsPopup.IsOpen = true;

    /// <summary>Fecha o menu depois de escolher uma opção (o comando do botão roda normalmente).</summary>
    private void OnToolsItemClick(object sender, RoutedEventArgs e) => ToolsPopup.IsOpen = false;
}
