namespace ColecaoMidias.Models;

public sealed class Album : System.ComponentModel.INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public MediaFormat Format { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public int? PurchaseYear { get; set; }
    public decimal Price { get; set; }

    /// <summary>Nome do arquivo da capa dentro da pasta de capas do app (não o caminho completo).</summary>
    public string? CoverFile { get; set; }
    public string? Notes { get; set; }

    /// <summary>true = está na lista de desejos (ainda não foi comprado). O "Valor" passa a ser o preço-alvo.</summary>
    public bool IsWishlist { get; set; }

    // ----- Notas pessoais e organização (v1.1) -----

    /// <summary>Gênero musical, texto livre (ex.: "Rock progressivo").</summary>
    public string? Genre { get; set; }

    /// <summary>Listas do usuário ("Favoritos", "Para ouvir"…), separadas por ";". Use <see cref="TagList"/> para ler.</summary>
    public string? Tags { get; set; }

    /// <summary>0 = sem nota; 1 a 5 estrelas.</summary>
    public int Rating { get; set; }

    /// <summary>Código de conservação do disco/fita/CD (M, NM, VG+, VG, G+, G, F, P) ou null.</summary>
    public string? MediaCondition { get; set; }

    /// <summary>Código de conservação da capa/encarte ou null.</summary>
    public string? CoverCondition { get; set; }

    public string? PurchasePlace { get; set; }

    private string? _tagSource;
    private IReadOnlyList<string> _tagList = Array.Empty<string>();

    public IReadOnlyList<string> TagList
    {
        get
        {
            if (!ReferenceEquals(_tagSource, Tags))
            {
                _tagList = TagUtil.Split(Tags);
                _tagSource = Tags;
            }
            return _tagList;
        }
    }

    public bool HasTag(string name) => TagList.Contains(name, StringComparer.OrdinalIgnoreCase);
    public bool IsFavorite => HasTag(TagUtil.Favorites);

    public int? Decade => Decades.Of(ReleaseDate);

    public bool HasGenre => !string.IsNullOrWhiteSpace(Genre);
    public bool HasTags => TagList.Count > 0;
    public bool HasRating => Rating is >= 1 and <= 5;
    public bool HasPlace => !string.IsNullOrWhiteSpace(PurchasePlace);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public bool HasMediaCondition => !string.IsNullOrEmpty(MediaCondition);
    public bool HasCoverCondition => !string.IsNullOrEmpty(CoverCondition);
    public bool HasConditions => HasMediaCondition || HasCoverCondition;

    /// <summary>"★★★★☆" (vazio quando não há nota).</summary>
    public string RatingText => HasRating ? new string('★', Rating) + new string('☆', 5 - Rating) : "";
    public string MediaConditionLabel => Conditions.Label(MediaCondition);
    public string CoverConditionLabel => Conditions.Label(CoverCondition);
    public string TagsDisplay => string.Join(", ", TagList);

    // Propriedades de apresentação (usadas pelas telas)
    public string FormatLabel => MediaFormatInfo.Label(Format);
    public string FormatDisplay => $"{MediaFormatInfo.Glyph(Format)} {MediaFormatInfo.Label(Format)}";
    public string ReleaseDisplay => Fmt.Date(ReleaseDate);
    public string PurchaseYearDisplay => PurchaseYear?.ToString() ?? "—";
    public string PriceDisplay => Fmt.Money(Price);
    public string SummaryLine => $"Compra: {PurchaseYearDisplay}  ·  {PriceDisplay}  ·  Lançamento: {ReleaseDisplay}";

    /// <summary>Capa já decodificada. Ligada com IsAsync=True na tela: carrega fora da thread da interface.</summary>
    public System.Windows.Media.ImageSource? CoverImage
    {
        get
        {
            // Já na memória: aparece na hora (trocar de filtro não pisca). Senão carrega em segundo plano uma única vez.
            var cached = Converters.CoverImageLoader.TryGetCached(CoverFile);
            if (cached is not null || string.IsNullOrWhiteSpace(CoverFile) || _coverRequested)
                return cached;

            _coverRequested = true;
            var context = SynchronizationContext.Current;
            var name = CoverFile;
            Task.Run(() =>
            {
                Converters.CoverImageLoader.Load(name);
                context?.Post(_ => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CoverImage))), null);
            });
            return null;
        }
    }

    private bool _coverRequested;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
