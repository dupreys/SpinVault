using ColecaoMidias.Models;
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
            var span = Math.Max(2, (int)(width / 180));
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
