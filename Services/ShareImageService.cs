using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ColecaoMidias.Converters;
using ColecaoMidias.Models;
using ColecaoMidias.Views;

namespace ColecaoMidias.Services;

/// <summary>Uma barra do cartão de compartilhamento (largura já em pixels).</summary>
public sealed record ShareBar(string Label, string Value, double Width);

/// <summary>Dados mostrados no cartão de compartilhamento (imagem 1080 x 1350).</summary>
public sealed class ShareCardModel
{
    public string DateText { get; init; } = "";
    public string ItemsText { get; init; } = "";
    public string ValueText { get; init; } = "";
    public string AverageText { get; init; } = "";
    public string VinylText { get; init; } = "";
    public string CdText { get; init; } = "";
    public string CassetteText { get; init; } = "";
    public IReadOnlyList<ShareBar> Artists { get; init; } = Array.Empty<ShareBar>();
    public IReadOnlyList<ShareBar> Years { get; init; } = Array.Empty<ShareBar>();
    public IReadOnlyList<ImageSource> Covers { get; init; } = Array.Empty<ImageSource>();
    public bool HasCovers => Covers.Count > 0;
    public bool HasArtists => Artists.Count > 0;
    public bool HasYears => Years.Count > 0;
}

/// <summary>Gera a imagem (PNG) do resumo da coleção para compartilhar.</summary>
public static class ShareImageService
{
    public const int Width = 1080, Height = 1350;
    private const double BarMaxWidth = 440;

    public static ShareCardModel BuildModel(IReadOnlyList<Album> albums, InsightsData data)
    {
        // Capas: as mais bem avaliadas e mais caras primeiro; só em múltiplos de 4 (grade 4 x N).
        var covers = new List<ImageSource>();
        foreach (var album in albums.Where(a => !string.IsNullOrWhiteSpace(a.CoverFile))
                                    .OrderByDescending(a => a.Rating).ThenByDescending(a => a.Price))
        {
            if (CoverImageLoader.Load(album.CoverFile) is { } image)
                covers.Add(image);
            if (covers.Count == 8)
                break;
        }
        covers = covers.Take(covers.Count / 4 * 4).ToList();

        var artists = data.TopArtistsByCount.Take(5).ToList();
        var maxCount = artists.Count == 0 ? 1 : artists.Max(a => a.Count);

        var years = data.SpendByYear.Where(y => y.Label != InsightsService.NoYearLabel).TakeLast(6).ToList();
        var maxSpend = years.Count == 0 ? 1m : Math.Max(years.Max(y => y.Total), 0.01m);

        return new ShareCardModel
        {
            DateText = data.Today.ToString("dd 'de' MMMM 'de' yyyy", Fmt.PtBr),
            ItemsText = data.Count.ToString(Fmt.PtBr),
            ValueText = Fmt.Money(data.TotalValue),
            AverageText = Fmt.Money(data.AverageTicket),
            VinylText = $"{data.Vinyl} vinis",
            CdText = $"{data.CD} CDs",
            CassetteText = $"{data.Cassette} fitas",
            Covers = covers,
            Artists = artists.Select(a => new ShareBar(a.Label, $"{a.Count} {(a.Count == 1 ? "álbum" : "álbuns")}",
                Math.Max(BarMaxWidth * a.Count / maxCount, 8))).ToList(),
            Years = years.Select(y => new ShareBar(y.Label, Fmt.Money(y.Total),
                Math.Max(BarMaxWidth * (double)(y.Total / maxSpend), 8))).ToList()
        };
    }

    /// <summary>Desenha o cartão fora da tela e salva como PNG (2x, para ficar nítido). Chamar na thread da interface.</summary>
    public static void SavePng(string path, ShareCardModel model)
    {
        var view = new ShareCardView { DataContext = model, Width = Width, Height = Height };
        view.Measure(new Size(Width, Height));
        view.Arrange(new Rect(0, 0, Width, Height));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);   // deixa os bindings e imagens se resolverem

        const double scale = 2;
        var bitmap = new RenderTargetBitmap((int)(Width * scale), (int)(Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(view);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }
}
