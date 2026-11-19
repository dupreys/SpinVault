using System.Collections.ObjectModel;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

public partial class AlbumEditViewModel : ObservableObject
{
    private readonly AlbumRepository _repository;
    private readonly Album? _original;
    private readonly bool _isWishlist;
    private readonly bool _isPurchase;

    private string? _pendingCoverPath;   // imagem escolhida, ainda não copiada para a pasta do app
    private bool _coverRemoved;

    /// <summary>Disparado com true ao salvar e false ao cancelar.</summary>
    public event Action<bool>? CloseRequested;

    public int? SavedId { get; private set; }

    public string WindowTitle =>
        _isPurchase ? Loc.T("Registrar compra")
        : _isWishlist ? (_original is null ? Loc.T("Novo desejo") : Loc.T("Editar desejo"))
        : _original is null ? Loc.T("Novo álbum") : Loc.T("Editar álbum");

    /// <summary>Na lista de desejos não existe "ano de compra" e o valor é o preço-alvo.</summary>
    public bool ShowPurchaseYear => !_isWishlist;
    public string PriceLabel => _isWishlist ? Loc.T("Preço-alvo (R$)") : Loc.T("Valor (R$)");

    [ObservableProperty] private string title = "";
    [ObservableProperty] private string artist = "";
    [ObservableProperty] private MediaFormat format = MediaFormat.Vinyl;
    [ObservableProperty] private string releaseDateText = "";
    [ObservableProperty] private string purchaseYearText = "";
    [ObservableProperty] private string priceText = "";
    [ObservableProperty] private string notes = "";

    // ----- Notas pessoais, gênero e listas (v1.1) -----
    [ObservableProperty] private string purchasePlace = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MediaConditionLabel))]
    private string mediaCondition = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoverConditionLabel))]
    private string coverCondition = "";

    [ObservableProperty] private int rating;
    [ObservableProperty] private string newTagText = "";

    public string MediaConditionLabel => MediaCondition.Length == 0 ? Loc.T("Sem informação") : Conditions.Label(MediaCondition);
    public string CoverConditionLabel => CoverCondition.Length == 0 ? Loc.T("Sem informação") : Conditions.Label(CoverCondition);

    /// <summary>Nota, conservação e local de compra só fazem sentido para o que já foi comprado.</summary>
    public bool ShowCollectionFields => !_isWishlist;

    public ObservableCollection<StarItem> Stars { get; } = new(Enumerable.Range(1, 5).Select(n => new StarItem(n)));
    public ObservableCollection<ChoiceItem> MediaConditions { get; } = new();
    public ObservableCollection<ChoiceItem> CoverConditions { get; } = new();
    public ObservableCollection<TagChoice> TagChoices { get; } = new();
    [ObservableProperty] private string errorMessage = "";

    /// <summary>Mensagem da busca de capa online (ex.: "Buscando capa…").</summary>
    [ObservableProperty] private string coverStatus = "";

    /// <summary>Mensagem da busca de dados do álbum (artista e lançamento).</summary>
    [ObservableProperty] private string lookupStatus = "";

    /// <summary>Link de uma imagem da internet colado pelo usuário.</summary>
    [ObservableProperty] private string imageUrl = "";

    /// <summary>Nome do arquivo (capa já salva) ou caminho completo (capa recém-escolhida).</summary>
    [ObservableProperty] private string? coverPreview;

    /// <param name="isWishlist">Edita um item da lista de desejos.</param>
    /// <param name="isPurchase">O item é da lista de desejos e está sendo comprado: ao salvar, vai para a coleção.</param>
    public AlbumEditViewModel(AlbumRepository repository, Album? original, bool isWishlist = false, bool isPurchase = false)
    {
        _repository = repository;
        _original = original;
        _isWishlist = isWishlist && !isPurchase;
        _isPurchase = isPurchase;

        BuildChoices(original);

        if (original is null)
        {
            if (!_isWishlist)
                PurchaseYearText = DateTime.Now.Year.ToString();
            return;
        }

        Title = original.Title;
        Artist = original.Artist;
        Format = original.Format;
        ReleaseDateText = original.ReleaseDate.HasValue ? Fmt.Date(original.ReleaseDate) : "";
        PurchaseYearText = original.PurchaseYear?.ToString() ?? "";
        PriceText = original.Price.ToString("0.00", Fmt.Current);
        Notes = original.Notes ?? "";
        CoverPreview = original.CoverFile;
        PurchasePlace = original.PurchasePlace ?? "";
        MediaCondition = original.MediaCondition ?? "";
        CoverCondition = original.CoverCondition ?? "";
        Rating = original.Rating;

        if (_isPurchase)
            PurchaseYearText = DateTime.Now.Year.ToString();   // compra feita agora; o preço-alvo vira o valor inicial
    }

    private void BuildChoices(Album? original)
    {
        foreach (var grade in new[] { new ConditionGrade("", "—") }.Concat(Conditions.All))
        {
            var tip = grade.Code.Length == 0 ? Loc.T("Sem informação") : grade.Label;

            var media = new ChoiceItem(grade.Code, grade.Code.Length == 0 ? "—" : grade.Code, tip)
                { IsSelected = grade.Code == (original?.MediaCondition ?? "") };
            media.Chosen += c => MediaCondition = c.Key;
            MediaConditions.Add(media);

            var cover = new ChoiceItem(grade.Code, grade.Code.Length == 0 ? "—" : grade.Code, tip)
                { IsSelected = grade.Code == (original?.CoverCondition ?? "") };
            cover.Chosen += c => CoverCondition = c.Key;
            CoverConditions.Add(cover);
        }

        // Listas: as padrão + todas que já existem + as deste álbum.
        var (_, knownTags) = _repository.GetKnownGenresAndTags();
        var selected = original?.TagList ?? Array.Empty<string>();
        foreach (var name in TagUtil.Defaults.Concat(knownTags).Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase))
            TagChoices.Add(new TagChoice(name, selected.Contains(name, StringComparer.OrdinalIgnoreCase)));

    }

    partial void OnRatingChanged(int value)
    {
        foreach (var star in Stars)
            star.Filled = star.Number <= value;
    }

    /// <summary>Clicar na estrela marca a nota; clicar de novo na mesma tira a nota.</summary>
    [RelayCommand]
    private void SetRating(int number) => Rating = Rating == number ? 0 : number;

    [RelayCommand]
    private void AddTag()
    {
        var name = TagUtil.Clean(NewTagText);
        if (name.Length == 0)
            return;

        var existing = TagChoices.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            TagChoices.Add(new TagChoice(name, true));
        else
            existing.IsSelected = true;

        NewTagText = "";
    }

    /// <summary>Busca artista e data de lançamento na internet só pelo nome do álbum.</summary>
    [RelayCommand]
    private async Task LookupAlbumAsync()
    {
        if (Title.Trim().Length == 0)
        {
            LookupStatus = Loc.T("Digite o nome do álbum para buscar.");
            return;
        }

        LookupStatus = Loc.T("Buscando dados do álbum…");

        using var service = new AlbumLookupService();
        var result = await service.SearchAsync(Title, Artist);

        if (result.Matches.Count == 0)
        {
            LookupStatus = result.Message;
            return;
        }

        // Um único resultado, ou um claramente melhor que os demais: usa direto. Senão o usuário escolhe.
        var best = result.Matches[0];
        var clear = result.Matches.Count == 1 ||
                    (best.Score >= 95 && result.Matches.Skip(1).All(m => m.Score < best.Score - 10));

        var chosen = clear ? best : Dialogs.PickAlbumMatch(result.Matches);
        if (chosen is null)
        {
            LookupStatus = Loc.T("Busca cancelada. Você pode preencher manualmente.");
            return;
        }

        Artist = chosen.Artist;
        if (chosen.HasDate)
            ReleaseDateText = chosen.ReleaseDateText;

        LookupStatus = chosen.HasDate
            ? Loc.F("Encontrado: {0}, {1}. Confira antes de salvar.", chosen.Artist, chosen.ReleaseDateText)
            : Loc.F("Encontrado: {0} (sem data cadastrada — preencha o lançamento).", chosen.Artist);

        // Aproveita para trazer a capa, se o álbum ainda não tem uma.
        if (CoverPreview is null)
        {
            using var covers = new CoverFetchService();
            var data = await covers.FindCoverByIdAsync(chosen.Id);
            if (data is not null && CoverPreview is null)
            {
                var temp = Path.Combine(Path.GetTempPath(), $"capa_{Guid.NewGuid():N}{CoverFetchService.ExtensionFor(data)}");
                await File.WriteAllBytesAsync(temp, data);
                SetCoverFile(temp);
                LookupStatus += Loc.T(" Capa incluída.");
            }
        }
    }

    /// <summary>Chamado pelo botão "Escolher capa" e pelo arrastar-e-soltar.</summary>
    public void SetCoverFile(string path)
    {
        if (!CoverService.IsSupportedImage(path))
        {
            ErrorMessage = Loc.T("Formato de imagem não suportado. Use JPG, PNG, BMP ou GIF.");
            return;
        }

        ErrorMessage = "";
        _pendingCoverPath = path;
        _coverRemoved = false;
        CoverPreview = path;
    }

    [RelayCommand]
    private void ChooseCover()
    {
        var path = Dialogs.PickImageFile();
        if (path != null)
            SetCoverFile(path);
    }

    /// <summary>Busca a capa na internet pelo artista e título digitados.</summary>
    [RelayCommand]
    private async Task FetchCoverAsync()
    {
        if (Title.Trim().Length == 0 || Artist.Trim().Length == 0)
        {
            CoverStatus = Loc.T("Preencha o álbum e o artista para buscar a capa.");
            return;
        }

        CoverStatus = Loc.T("Buscando capa…");

        using var service = new CoverFetchService();
        var result = await service.FindCoverAsync(Artist, Title);

        if (!result.Found)
        {
            CoverStatus = result.Message;
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"capa_{Guid.NewGuid():N}{CoverFetchService.ExtensionFor(result.Data!)}");
        await File.WriteAllBytesAsync(temp, result.Data!);

        SetCoverFile(temp);    // a cópia definitiva para a pasta do app acontece ao clicar em Salvar
        CoverStatus = Loc.T("Capa encontrada! Clique em Salvar para guardar.");
    }

    /// <summary>Baixa a capa a partir do link colado (ex.: "Copiar endereço da imagem" no Google Imagens).</summary>
    [RelayCommand]
    private async Task DownloadFromUrlAsync()
    {
        if (ImageUrl.Trim().Length == 0)
        {
            CoverStatus = Loc.T("Cole primeiro o link da imagem.");
            return;
        }

        CoverStatus = Loc.T("Baixando imagem…");

        using var service = new ImageDownloadService();
        var result = await service.DownloadAsync(ImageUrl);

        if (!result.Found)
        {
            CoverStatus = result.Message;
            return;
        }

        var data = result.Data!;
        var extension = ImageDownloadService.ExtensionFor(data);

        // O app guarda JPG ou PNG; GIF, BMP e WebP são convertidos para PNG.
        if (extension is not (".jpg" or ".png"))
        {
            var png = ImageConversion.ToPng(data);
            if (png is null)
            {
                CoverStatus = Loc.T("Não consegui abrir esse formato de imagem (WebP?). Tente outra imagem.");
                return;
            }

            data = png;
            extension = ".png";
        }

        var temp = Path.Combine(Path.GetTempPath(), $"capa_{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(temp, data);

        SetCoverFile(temp);
        ImageUrl = "";
        CoverStatus = Loc.T("Imagem carregada! Clique em Salvar para guardar.");
    }

    /// <summary>Usado ao arrastar uma imagem/link do navegador para a janela.</summary>
    public void UseImageUrl(string url)
    {
        ImageUrl = url.Trim();
        DownloadFromUrlCommand.Execute(null);
    }

    [RelayCommand]
    private void RemoveCover()
    {
        _pendingCoverPath = null;
        _coverRemoved = true;
        CoverPreview = null;
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = "";

        var albumTitle = Title.Trim();
        var albumArtist = Artist.Trim();

        if (albumTitle.Length == 0) { ErrorMessage = Loc.T("Informe o nome do álbum."); return; }
        if (albumArtist.Length == 0) { ErrorMessage = Loc.T("Informe o artista."); return; }

        DateTime? release = null;
        if (ReleaseDateText.Trim().Length > 0)
        {
            if (!Fmt.TryParseDate(ReleaseDateText, out var parsedDate))
            {
                ErrorMessage = Loc.T("Data de lançamento inválida. Use dd/mm/aaaa (ou apenas o ano).");
                return;
            }
            release = parsedDate;
        }

        int? purchaseYear = null;
        if (!_isWishlist && PurchaseYearText.Trim().Length > 0)
        {
            if (!int.TryParse(PurchaseYearText.Trim(), out var year) || year < 1900 || year > DateTime.Now.Year + 1)
            {
                ErrorMessage = Loc.T("Ano de compra inválido.");
                return;
            }
            purchaseYear = year;
        }

        decimal price = 0m;
        if (PriceText.Trim().Length > 0)
        {
            if (!Fmt.TryParseMoney(PriceText, out price) || price < 0)
            {
                ErrorMessage = Loc.T("Valor inválido. Exemplo: 45,90");
                return;
            }
        }

        if (_repository.Exists(albumTitle, albumArtist, Format, _original?.Id, _isWishlist) &&
            !Dialogs.Confirm(Loc.T("Álbum duplicado"),
                Loc.F("Você já {0} \"{1}\" de {2} em {3}.\nSalvar mesmo assim?", (_isWishlist ? Loc.T("tem na lista de desejos") : Loc.T("tem")), albumTitle, albumArtist, MediaFormatInfo.Label(Format))))
        {
            return;
        }

        try
        {
            var coverFile = _original?.CoverFile;
            string? coverToDelete = null;

            if (_pendingCoverPath != null)
            {
                coverToDelete = coverFile;
                coverFile = CoverService.Import(_pendingCoverPath);
            }
            else if (_coverRemoved)
            {
                coverToDelete = coverFile;
                coverFile = null;
            }

            var album = new Album
            {
                Id = _original?.Id ?? 0,
                Title = albumTitle,
                Artist = albumArtist,
                Format = Format,
                ReleaseDate = release,
                PurchaseYear = purchaseYear,
                Price = price,
                CoverFile = coverFile,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                IsWishlist = _isWishlist,
                Genre = _original?.Genre,   // o gênero não é mais editado aqui; o que já estava salvo é mantido
                Tags = TagUtil.Join(TagChoices.Where(t => t.IsSelected).Select(t => t.Name)),
                Rating = Rating,
                MediaCondition = MediaCondition.Length == 0 ? null : MediaCondition,
                CoverCondition = CoverCondition.Length == 0 ? null : CoverCondition,
                PurchasePlace = string.IsNullOrWhiteSpace(PurchasePlace) ? null : PurchasePlace.Trim(),

                // O empréstimo é gerenciado pelos botões Emprestar/Devolver; o editor só preserva.
                LoanedTo = _original?.LoanedTo,
                LoanedOn = _original?.LoanedOn
            };

            if (_original is null)
                _repository.Add(album);
            else
                _repository.Update(album);

            CoverService.Delete(coverToDelete);

            SavedId = album.Id;
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = Loc.T("Não foi possível salvar: ") + ex.Message;
        }
    }
}
