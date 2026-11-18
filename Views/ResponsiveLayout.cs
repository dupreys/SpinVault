using System.Windows;
using System.Windows.Controls.Primitives;

namespace ColecaoMidias.Views;

/// <summary>Ajustes de layout que dependem da largura da janela.</summary>
public static class ResponsiveLayout
{
    /// <summary>
    /// Define o número de colunas de um UniformGrid e recalcula o espaçamento entre os cartões
    /// (margens por posição), para que fiquem alinhados em qualquer número de colunas.
    /// </summary>
    /// <param name="gap">Espaço entre cartões, na horizontal e na vertical.</param>
    /// <param name="bottomGap">Espaço abaixo da última linha (quando o que vem depois precisa de respiro).</param>
    public static void SetColumns(UniformGrid grid, int columns, double gap, double bottomGap = 0)
    {
        columns = Math.Max(1, columns);
        grid.Columns = columns;

        var count = grid.Children.Count;
        var rows = (int)Math.Ceiling(count / (double)columns);
        for (var i = 0; i < count; i++)
        {
            if (grid.Children[i] is not FrameworkElement card)
                continue;

            var column = i % columns;
            var row = i / columns;
            card.Margin = new Thickness(
                column == 0 ? 0 : gap / 2,
                0,
                column == columns - 1 ? 0 : gap / 2,
                row < rows - 1 ? gap : bottomGap);
        }
    }

    /// <summary>Largura do painel de detalhes conforme o espaço disponível.</summary>
    public static double DetailsWidth(double availableWidth) =>
        availableWidth >= 1000 ? 300 : availableWidth >= 780 ? 260 : 220;
}
