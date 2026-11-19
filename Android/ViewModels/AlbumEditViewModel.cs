using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpinVaultMobile.ViewModels;

/// <summary>Cadastro e edição de um álbum (ou de um desejo).</summary>
public sealed partial class AlbumEditViewModel : ObservableObject
{
    private static readonly MediaFormat[] Formats = { MediaFormat.Vinyl, MediaFormat.CD, MediaFormat.Cassette };

    private readonly Album? _original;
    private readonly bool _wishlist;
    private string? _coverFile;
    private string? _oldCoverToDelete;

    public AlbumEditViewModel(Album? original, bool wishlist)
    {
        _original = original;
        _wishlist = original?.IsWishlist ?? wishlist;

        FormatLabels = Formats.Select(MediaFormatInfo.Label).ToList();
        RatingLabels = new List<string> { "—", "★", "★★", "★★★", "★★★★", "★★★★★" };
        // O primeiro item ("—") é "sem avaliação"; os demais são os graus M, NM, VG+, VG, G+, G, F, P.
        ConditionLabels = new[] { "—" }.Concat(Conditions.All.Select(c => c.Label)).ToList();

        if (original is null)
            return;

        title = original.Title;
        artist = original.Artist;
        formatIndex = Math.Max(0, Array.IndexOf(Formats, original.Format));
        releaseDateText = original.ReleaseDate.HasValue ? Fmt.Date(original.ReleaseDate) : "";
        purchaseYearText = original.PurchaseYear?.ToString() ?? "";
        priceText = original.Price > 0 ? original.Price.ToString("0.00", Fmt.Current) : "";
        purchasePlace = original.PurchasePlace ?? "";
        notes = original.Notes ?? "";
        ratingIndex = Math.Clamp(original.Rating, 0, 5);
        mediaConditionIndex = IndexOfCondition(original.MediaCondition);
        coverConditionIndex = IndexOfCondition(original.CoverCondition);
        _coverFile = original.CoverFile;
    }

    public bool IsWishlist => _wishlist;
    public bool IsNotWishlist => !_wishlist;

    public string PageTitle => (_original, _wishlist) switch
    {
        (null, false) => Loc.T("Novo álbum"),
        (null, true) => Loc.T("Novo desejo"),
        (_, false) => Loc.T("Editar álbum"),
        _ => Loc.T("Editar desejo"),
    };

    public string PriceLabel => _wishlist ? Loc.T("Preço-alvo (R$)") : Loc.T("Valor (R$)");

    public IReadOnlyList<string> FormatLabels { get; }
    public IReadOnlyList<string> RatingLabels { get; }
    public IReadOnlyList<string> ConditionLabels { get; }

    private static int IndexOfCondition(string? code)
    {
        var i = Conditions.All.ToList().FindIndex(c => c.Code == code);
        return i < 0 ? 0 : i + 1;
    }

    private static string? ConditionAt(int index) =>
        index <= 0 || index > Conditions.All.Count ? null : Conditions.All[index - 1].Code;

    [ObservableProperty] private string title = "";
    [ObservableProperty] private string artist = "";
    [ObservableProperty] private int formatIndex;
    [ObservableProperty] private string releaseDateText = "";
    [ObservableProperty] private string purchaseYearText = "";
    [ObservableProperty] private string priceText = "";
    [ObservableProperty] private int mediaConditionIndex;
    [ObservableProperty] private int coverConditionIndex;
    [ObservableProperty] private string purchasePlace = "";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private int ratingIndex;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? coverPath;
    [ObservableProperty] private bool hasCover;

    public void LoadCover()
    {
        CoverPath = CoverService.Resolve(_coverFile);
        HasCover = CoverPath is not null;
    }

    /// <summary>Guarda a imagem escolhida na galeria como capa do álbum.</summary>
    public void SetCover(byte[] data, string extension)
    {
        var (small, ext) = ImageHelper.Shrink(data, string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant());
        var newFile = CoverService.SaveBytes(small, ext);
        if (_coverFile is not null && _coverFile != _original?.CoverFile)
            CoverService.Delete(_coverFile);           // capa nova escolhida antes e já substituída
        else if (_original?.CoverFile is not null)
            _oldCoverToDelete = _original.CoverFile;   // a capa antiga só some quando salvar
        _coverFile = newFile;
        LoadCover();
    }

    public void RemoveCover()
    {
        if (_coverFile is not null && _coverFile != _original?.CoverFile)
            CoverService.Delete(_coverFile);
        else if (_original?.CoverFile is not null)
            _oldCoverToDelete = _original.CoverFile;
        _coverFile = null;
        LoadCover();
    }

    /// <summary>Busca a capa na internet (MusicBrainz / Cover Art Archive).</summary>
    public async Task FetchCoverAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Artist))
        {
            StatusText = Loc.T("Preencha o álbum e o artista para buscar a capa.");
            return;
        }

        IsBusy = true;
        StatusText = Loc.T("Buscando capa…");
        try
        {
            using var service = new CoverFetchService();
            var result = await service.FindCoverAsync(Artist.Trim(), Title.Trim());
            if (result.Found)
            {
                SetCover(result.Data!, CoverFetchService.ExtensionFor(result.Data!));
                StatusText = Loc.T("Capa encontrada.");
            }
            else
            {
                StatusText = result.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Busca artista e data de lançamento pelo nome do álbum. Devolve null se não escolheu nada.</summary>
    public async Task<AlbumMatch?> LookupAsync(Func<IReadOnlyList<AlbumMatch>, Task<AlbumMatch?>> choose)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusText = Loc.T("Digite o nome do álbum para buscar.");
            return null;
        }

        IsBusy = true;
        StatusText = Loc.T("Buscando dados do álbum…");
        try
        {
            using var service = new AlbumLookupService();
            var result = await service.SearchAsync(Title.Trim(), Artist.Trim());
            if (result.Matches.Count == 0)
            {
                StatusText = result.Message;
                return null;
            }

            var match = AlbumLookupService.PickConfident(result.Matches, Artist.Trim());
            match ??= await choose(result.Matches);
            if (match is null)
            {
                StatusText = Loc.T("Cancelado.");
                return null;
            }

            Artist = match.Artist;
            if (match.HasDate)
                ReleaseDateText = match.ReleaseDateText;
            StatusText = Loc.F("Encontrado: {0}, {1}. Confira antes de salvar.", match.Artist, match.ReleaseDateText);
            return match;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Valida e monta o álbum a gravar. Devolve null (e escreve o motivo em StatusText) se algo estiver errado.</summary>
    public Album? Build()
    {
        if (string.IsNullOrWhiteSpace(Title)) { StatusText = Loc.T("Informe o nome do álbum."); return null; }
        if (string.IsNullOrWhiteSpace(Artist)) { StatusText = Loc.T("Informe o artista."); return null; }

        DateTime? release = null;
        if (ReleaseDateText.Trim().Length > 0)
        {
            if (!Fmt.TryParseDate(ReleaseDateText, out var parsedDate))
            {
                StatusText = Loc.T("Data de lançamento inválida. Use dd/mm/aaaa (ou apenas o ano).");
                return null;
            }
            release = parsedDate;
        }

        int? year = null;
        if (PurchaseYearText.Trim().Length > 0)
        {
            if (!int.TryParse(PurchaseYearText.Trim(), out var parsedYear) || parsedYear < 1900 || parsedYear > 2100)
            {
                StatusText = Loc.T("Ano de compra inválido.");
                return null;
            }
            year = parsedYear;
        }
        else if (!_wishlist)
        {
            year = DateTime.Today.Year;
        }

        decimal price = 0;
        if (PriceText.Trim().Length > 0 && !Fmt.TryParseMoney(PriceText, out price))
        {
            StatusText = Loc.T("Valor inválido. Exemplo: 45,90");
            return null;
        }

        // Edita o álbum original (para não perder listas, conservação e empréstimo) ou cria um novo.
        var album = _original ?? new Album { IsWishlist = _wishlist };
        album.Title = Title.Trim();
        album.Artist = Artist.Trim();
        album.Format = Formats[Math.Clamp(FormatIndex, 0, Formats.Length - 1)];
        album.ReleaseDate = release;
        album.PurchaseYear = year;
        album.Price = price;
        album.PurchasePlace = string.IsNullOrWhiteSpace(PurchasePlace) ? null : PurchasePlace.Trim();
        album.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        album.Rating = Math.Clamp(RatingIndex, 0, 5);
        if (!_wishlist)
        {
            album.MediaCondition = ConditionAt(MediaConditionIndex);
            album.CoverCondition = ConditionAt(CoverConditionIndex);
        }
        album.CoverFile = _coverFile;
        return album;
    }

    public bool IsDuplicate(Album album) =>
        AppData.Repo.Exists(album.Title, album.Artist, album.Format, _original?.Id, _wishlist);

    public string DuplicateMessage(Album album) =>
        Loc.F("Você já {0} \"{1}\" de {2} em {3}.\nSalvar mesmo assim?",
            _wishlist ? Loc.T("tem na lista de desejos") : Loc.T("tem"),
            album.Title, album.Artist, MediaFormatInfo.Label(album.Format));

    public void Save(Album album)
    {
        if (_original is null)
            AppData.Repo.Add(album);
        else
            AppData.Repo.Update(album);

        if (_oldCoverToDelete is not null && _oldCoverToDelete != album.CoverFile)
            CoverService.Delete(_oldCoverToDelete);
    }

    /// <summary>Saiu sem salvar: apaga a capa nova que ficou solta na pasta.</summary>
    public void DiscardUnsavedCover()
    {
        if (_coverFile is not null && _coverFile != _original?.CoverFile)
            CoverService.Delete(_coverFile);
    }
}
