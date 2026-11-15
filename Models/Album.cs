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
