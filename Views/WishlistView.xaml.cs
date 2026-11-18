using System.Windows;
using System.Windows.Controls;

namespace ColecaoMidias.Views;

public partial class WishlistView : UserControl
{
    public WishlistView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Em janela estreita o painel de detalhes fica mais fino para sobrar espaço para as capas.</summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        DetailsPanel.Width = ResponsiveLayout.DetailsWidth(width);
        DetailsPanel.Margin = new Thickness(width >= 780 ? 18 : 12, 0, 0, 0);
        DetailsPanel.Padding = new Thickness(width >= 780 ? 18 : 14);
    }
}
