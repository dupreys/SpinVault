using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpinVaultMobile.ViewModels;

public sealed record InfoRow(string Label, string Value);

/// <summary>Detalhes de um álbum, com as ações Favoritar, Editar, Excluir e "Comprei!".</summary>
public sealed partial class AlbumDetailViewModel : ObservableObject
{
    private readonly int _id;
    private readonly bool _wishlist;

    public AlbumDetailViewModel(Album album)
    {
        _id = album.Id;
        _wishlist = album.IsWishlist;
        this.album = album;
        Rebuild();
    }

    [ObservableProperty] private Album album;
    [ObservableProperty] private IReadOnlyList<InfoRow> rows = Array.Empty<InfoRow>();

    public bool IsWishlist => _wishlist;
    public bool IsNotWishlist => !_wishlist;
    public string FavoriteText => Album.IsFavorite ? "♥  " + Loc.T("Favorito") : "♡  " + Loc.T("Favoritar");

    /// <summary>Relê o álbum do banco (depois de editar) e monta de novo as linhas de informação.</summary>
    public void Reload()
    {
        var fresh = AppData.Repo.GetAll(_wishlist).FirstOrDefault(a => a.Id == _id);
        if (fresh is not null)
            Album = fresh;
        Rebuild();
    }

    private void Rebuild()
    {
        var a = Album;
        var list = new List<InfoRow>
        {
            new(Loc.T("Mídia"), a.FormatDisplay),
        };

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                list.Add(new InfoRow(label, value!));
        }

        Add(Loc.T("Lançamento"), a.ReleaseDate.HasValue ? a.ReleaseDisplay : null);
        if (_wishlist)
            Add(Loc.T("Preço-alvo"), a.Price > 0 ? a.PriceDisplay : null);
        else
        {
            Add(Loc.T("Ano de compra"), a.PurchaseYear?.ToString());
            Add(Loc.T("Valor (R$)"), a.Price > 0 ? a.PriceDisplay : null);
        }

        Add(Loc.T("Gênero"), a.Genre);
        Add(Loc.T("Listas"), a.HasTags ? a.TagsDisplay : null);
        Add(Loc.T("Minha nota"), a.HasRating ? a.RatingText : null);
        Add(Loc.T("Conservação do disco / fita"), a.MediaCondition is null ? null : a.MediaConditionLabel);
        Add(Loc.T("Conservação da capa / encarte"), a.CoverCondition is null ? null : a.CoverConditionLabel);
        Add(Loc.T("Local de compra"), a.PurchasePlace);
        Add(Loc.T("Emprestado"), a.IsLoaned ? a.LoanLabel : null);
        Add(Loc.T("Observações"), a.Notes);

        Rows = list;
        OnPropertyChanged(nameof(FavoriteText));
    }

    public void ToggleFavorite()
    {
        var tags = Album.TagList.ToList();
        if (Album.IsFavorite)
            tags.RemoveAll(t => t.Equals(TagUtil.Favorites, StringComparison.OrdinalIgnoreCase));
        else
            tags.Add(TagUtil.Favorites);

        Album.Tags = TagUtil.Join(tags);
        AppData.Repo.Update(Album);
        Reload();
    }

    public void Delete()
    {
        AppData.Repo.Delete(Album.Id);
        CoverService.Delete(Album.CoverFile);
    }

    /// <summary>"Comprei!": o desejo vira item da coleção, comprado neste ano.</summary>
    public void MarkAsBought()
    {
        Album.IsWishlist = false;
        Album.PurchaseYear ??= DateTime.Today.Year;
        AppData.Repo.Update(Album);
    }
}
