using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColecaoMidias.Views;

public partial class DashboardView : UserControl
{
    private bool _narrow;

    public DashboardView() => InitializeComponent();

    /// <summary>
    /// Layout responsivo: em janela larga formato, artistas e anos ficam lado a lado;
    /// em janela estreita empilham, e os indicadores passam para 2 colunas.
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 1250;
        KpiGrid.Columns = e.NewSize.Width < 1000 ? 2 : 4;

        if (narrow == _narrow)
            return;
        _narrow = narrow;

        if (narrow)
        {
            Place(FormatCard, 0, 0, 5, 0);
            Place(ArtistCard, 1, 0, 5, 12);
            Place(YearCard, 2, 0, 5, 12);
        }
        else
        {
            Place(FormatCard, 0, 0, 1, 0);
            Place(ArtistCard, 0, 2, 1, 0);
            Place(YearCard, 0, 4, 1, 0);
        }
    }

    private static void Place(FrameworkElement card, int row, int column, int columnSpan, double marginTop)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        Grid.SetColumnSpan(card, columnSpan);
        card.Margin = new Thickness(0, marginTop, 0, 0);
    }

    /// <summary>A roda do mouse sobre a faixa de capas rola o painel (Shift rola a faixa para os lados).</summary>
    private void OnStripWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            StripScroll.ScrollToHorizontalOffset(StripScroll.HorizontalOffset - e.Delta);
        else
            PageScroll.ScrollToVerticalOffset(PageScroll.VerticalOffset - e.Delta);
    }
}
