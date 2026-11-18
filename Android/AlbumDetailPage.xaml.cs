using ColecaoMidias.Models;
using ColecaoMidias.Services;
using SpinVaultMobile.ViewModels;

namespace SpinVaultMobile;

[QueryProperty(nameof(AlbumArg), "Album")]
public partial class AlbumDetailPage : ContentPage
{
    private AlbumDetailViewModel? _vm;

    public AlbumDetailPage()
    {
        InitializeComponent();
    }

    public Album? AlbumArg
    {
        get => _vm?.Album;
        set
        {
            if (value is null)
                return;
            _vm = new AlbumDetailViewModel(value);
            BindingContext = _vm;
        }
    }

    // Ao voltar da edição, relê o álbum do banco.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm?.Reload();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_vm is null)
            return;

        await Shell.Current.GoToAsync(nameof(AlbumEditPage),
            new Dictionary<string, object> { ["Album"] = _vm.Album, ["Wishlist"] = _vm.IsWishlist });
    }

    private void OnFavoriteClicked(object? sender, EventArgs e) => _vm?.ToggleFavorite();

    private async void OnBoughtClicked(object? sender, EventArgs e)
    {
        if (_vm is null)
            return;

        _vm.MarkAsBought();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_vm is null)
            return;

        var message = Loc.F("Excluir \"{0}\" ({1})?\nEssa ação não pode ser desfeita.", _vm.Album.Title, _vm.Album.FormatLabel);
        if (!await DisplayAlert(Loc.T("Excluir álbum"), message, Loc.T("Excluir"), Loc.T("Cancelar")))
            return;

        _vm.Delete();
        await Shell.Current.GoToAsync("..");
    }
}
