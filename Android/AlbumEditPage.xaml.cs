using ColecaoMidias.Models;
using ColecaoMidias.Services;
using SpinVaultMobile.ViewModels;

namespace SpinVaultMobile;

[QueryProperty(nameof(AlbumArg), "Album")]
[QueryProperty(nameof(WishlistArg), "Wishlist")]
public partial class AlbumEditPage : ContentPage
{
    private Album? _album;
    private bool _wishlist;
    private AlbumEditViewModel _vm;
    private bool _saved;

    public AlbumEditPage()
    {
        InitializeComponent();
        _vm = new AlbumEditViewModel(null, false);
        BindingContext = _vm;
    }

    // Os dois parâmetros chegam separados; a tela é montada de novo a cada um (barato, só campos de texto).
    public Album? AlbumArg
    {
        get => _album;
        set { _album = value; Rebuild(); }
    }

    public bool WishlistArg
    {
        get => _wishlist;
        set { _wishlist = value; Rebuild(); }
    }

    private void Rebuild()
    {
        _vm = new AlbumEditViewModel(_album, _wishlist);
        BindingContext = _vm;
        _vm.LoadCover();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCover();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_saved)
            _vm.DiscardUnsavedCover();
    }

    private async void OnPickCoverClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Loc.T("Escolha a capa do álbum"),
                FileTypes = FilePickerFileType.Images,
            });
            if (file is null)
                return;

            using var input = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            _vm.SetCover(memory.ToArray(), Path.GetExtension(file.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _vm.StatusText = ex.Message;
        }
    }

    private async void OnFetchCoverClicked(object? sender, EventArgs e) => await _vm.FetchCoverAsync();

    private void OnRemoveCoverClicked(object? sender, EventArgs e) => _vm.RemoveCover();

    private async void OnLookupClicked(object? sender, EventArgs e)
    {
        await _vm.LookupAsync(async matches =>
        {
            var options = matches.Take(8).Select(m => $"{m.Title} — {m.Subtitle}").ToArray();
            var chosen = await DisplayActionSheet(Loc.T("Qual é o seu álbum?"), Loc.T("Cancelar"), null, options);
            var index = Array.IndexOf(options, chosen);
            return index >= 0 ? matches[index] : null;
        });
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var album = _vm.Build();
        if (album is null)
            return;

        if (_vm.IsDuplicate(album) &&
            !await DisplayAlert(Loc.T("Álbum duplicado"), _vm.DuplicateMessage(album), Loc.T("Salvar"), Loc.T("Cancelar")))
            return;

        _vm.Save(album);
        _saved = true;
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
