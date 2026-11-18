using System.Collections.ObjectModel;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

/// <summary>Álbuns que o usuário quer comprar, com o preço-alvo de cada um.</summary>
public partial class WishlistViewModel : ObservableObject
{
    private readonly AlbumRepository _repository;

    public ObservableCollection<Album> Items { get; } = new();

    /// <summary>Disparado quando um desejo foi comprado e passou para a coleção.</summary>
    public event Action<int?>? Purchased;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(NoSelection))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(BoughtCommand))]
    private Album? selectedItem;

    [ObservableProperty] private string summaryText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool isEmpty = true;

    public bool HasItems => !IsEmpty;
    public bool HasSelection => SelectedItem != null;
    public bool NoSelection => SelectedItem == null;

    public WishlistViewModel(AlbumRepository repository)
    {
        _repository = repository;
        Reload();
    }

    public void Reload(int? selectId = null)
    {
        var all = _repository.GetAll(wishlist: true);

        Items.Clear();
        foreach (var item in all)
            Items.Add(item);

        IsEmpty = Items.Count == 0;
        SummaryText = Items.Count == 0
            ? Loc.T("Nenhum álbum na lista")
            : Loc.F("{0} álbuns · {1} em preços-alvo", Items.Count, Fmt.Money(Items.Sum(a => a.Price)));

        SelectedItem = selectId is { } id ? Items.FirstOrDefault(a => a.Id == id) : null;
    }

    [RelayCommand]
    private void Add()
    {
        var editor = new AlbumEditViewModel(_repository, null, isWishlist: true);
        if (Dialogs.ShowAlbumEditor(editor))
            Reload(editor.SavedId);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (SelectedItem is null)
            return;

        var editor = new AlbumEditViewModel(_repository, SelectedItem, isWishlist: true);
        if (Dialogs.ShowAlbumEditor(editor))
            Reload(editor.SavedId);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedItem is not { } item)
            return;

        if (!Dialogs.Confirm(Loc.T("Remover da lista"), Loc.F("Remover \"{0}\" ({1}) da lista de desejos?", item.Title, item.Artist)))
            return;

        _repository.Delete(item.Id);
        CoverService.Delete(item.CoverFile);
        Reload();
    }

    /// <summary>"Comprei!": abre o editor para confirmar valor e ano; ao salvar o álbum vai para a coleção.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Bought()
    {
        if (SelectedItem is not { } item)
            return;

        var editor = new AlbumEditViewModel(_repository, item, isWishlist: true, isPurchase: true);
        if (!Dialogs.ShowAlbumEditor(editor))
            return;

        Reload();
        Purchased?.Invoke(editor.SavedId);
    }
}
