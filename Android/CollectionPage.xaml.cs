using ColecaoMidias.Models;
using ColecaoMidias.Services;
using SpinVaultMobile.ViewModels;

namespace SpinVaultMobile;

public partial class CollectionPage : ContentPage
{
    private readonly CollectionViewModel _vm;

    public CollectionPage() : this(false)
    {
    }

    protected CollectionPage(bool wishlist)
    {
        InitializeComponent();
        _vm = new CollectionViewModel(wishlist);
        BindingContext = _vm;
        ApplyViewMode(refresh: false);
    }

    private AlbumTemplateSelector Selector => (AlbumTemplateSelector)Resources["AlbumSelector"];

    // Troca entre capas (grade) e lista, sem mexer no resto da tela.
    private void ApplyViewMode(bool refresh)
    {
        Selector.UseList = _vm.IsListView;
        AlbumList.ItemsLayout = _vm.IsListView
            ? new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 8 }
            : new GridItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                Span = SpanFor(Width),
                HorizontalItemSpacing = 12,
                VerticalItemSpacing = 12,
            };
        if (refresh)
            _vm.Refresh();   // recria a lista com o modelo de item escolhido
    }

    private static int SpanFor(double width) => width > 0 ? Math.Max(2, (int)(width / 180)) : 2;

    private void OnViewClicked(object? sender, EventArgs e)
    {
        _vm.IsListView = !_vm.IsListView;
        ApplyViewMode(refresh: true);
    }

    private async void OnSortClicked(object? sender, EventArgs e)
    {
        var options = CollectionOrdering.SortOptions;
        var labels = options.Select(o => (o.Key == _vm.SortKey ? "●  " : "     ") + o.Label).ToList();
        labels.Add(Loc.T("Agrupar por") + "…");

        var cancel = Loc.T("Cancelar");
        var choice = await DisplayActionSheet(Loc.T("Ordenar"), cancel, null, labels.ToArray());
        var index = choice is null || choice == cancel ? -1 : labels.IndexOf(choice);
        if (index < 0)
            return;

        if (index < options.Length)
            _vm.SetSort(options[index].Key);
        else
            await ChooseGroupAsync();
    }

    private async Task ChooseGroupAsync()
    {
        var options = CollectionOrdering.GroupOptions;
        var labels = options.Select(o => (o.Key == _vm.GroupKey ? "●  " : "     ") + o.Label).ToArray();

        var cancel = Loc.T("Cancelar");
        var choice = await DisplayActionSheet(Loc.T("Agrupar por"), cancel, null, labels);
        var index = choice is null || choice == cancel ? -1 : Array.IndexOf(labels, choice);
        if (index >= 0)
            _vm.SetGroup(options[index].Key);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Reload();
    }

    // Quantas capas cabem por linha depende da largura da tela (2 no celular, mais no tablet ou deitado).
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && AlbumList.ItemsLayout is GridItemsLayout grid)
        {
            var span = SpanFor(width);
            if (grid.Span != span)
                grid.Span = span;
        }
    }

    private void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: FormatChip chip })
            _vm.SelectChip(chip);
    }

    private async void OnAlbumSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Album album)
            return;

        AlbumList.SelectedItem = null;
        await Shell.Current.GoToAsync(nameof(AlbumDetailPage), new Dictionary<string, object> { ["Album"] = album });
    }

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(AlbumEditPage), new Dictionary<string, object> { ["Wishlist"] = _vm.IsWishlist });
}

/// <summary>A lista de desejos usa a mesma tela da coleção, só que mostra os álbuns ainda não comprados.</summary>
public sealed class WishlistPage : CollectionPage
{
    public WishlistPage() : base(true)
    {
    }
}

/// <summary>Escolhe o modelo do item: capa grande (grade) ou linha compacta (lista).</summary>
public sealed class AlbumTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GridTemplate { get; set; }
    public DataTemplate? ListTemplate { get; set; }
    public bool UseList { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (UseList ? ListTemplate : GridTemplate)!;
}
