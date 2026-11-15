using System.Windows;
using System.Windows.Controls;

namespace ColecaoMidias.Views;

public partial class CollectionView : UserControl
{
    public CollectionView() => InitializeComponent();

    private void OnFilterClick(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = true;

    private void OnToolsClick(object sender, RoutedEventArgs e) => ToolsPopup.IsOpen = true;

    /// <summary>Fecha o menu depois de escolher uma opção (o comando do botão roda normalmente).</summary>
    private void OnToolsItemClick(object sender, RoutedEventArgs e) => ToolsPopup.IsOpen = false;
}
