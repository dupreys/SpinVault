using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

/// <summary>Item de uma barra de ranking (rótulo, valor e o máximo para calcular a largura).</summary>
/// <param name="IsSelected">É o filtro ativo desta barra.</param>
/// <param name="Dim">Opacidade: 1 normal, menor quando outro item está selecionado.</param>
public sealed record BarItem(string Label, double Value, double Max, string Display, bool IsSelected = false, double Dim = 1);

/// <summary>Etiqueta de filtro ativo (ex.: "2025", "CD", "Angra") com o botão de remover.</summary>
public sealed record FilterChip(string Kind, string Text);

/// <summary>Fatia da rosca de formatos (também serve de legenda).</summary>
public sealed class DonutSlice
{
    public Geometry Data { get; init; } = Geometry.Empty;
    public MediaFormat Format { get; init; }
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public string PercentText { get; init; } = "";
    public bool IsSelected { get; init; }
    public double Dim { get; init; } = 1;
    public string Tip => $"{Label}: {Count} itens ({PercentText})";
}

/// <summary>Ponto do gráfico de aquisições por ano; as posições já vêm calculadas (em pixels do desenho).</summary>
public sealed class YearPoint
{
    public string Label { get; init; } = "";
    public string ValueText { get; init; } = "";
    public double DotLeft { get; init; }
    public double DotTop { get; init; }
    public double ValueLeft { get; init; }
    public double ValueTop { get; init; }
    public double LabelLeft { get; init; }
    public double LabelTop { get; init; }
    public bool IsSelected { get; init; }
    public double Dim { get; init; } = 1;
    public string Tip => $"{Label}: {ValueText} itens";
}

public partial class DashboardViewModel : ObservableObject
{
    // Área de desenho do gráfico de linha (o DashboardView usa 460 x 210).
    private const double PlotLeft = 30, PlotRight = 430, PlotTop = 38, PlotBottom = 168, LabelTop = 180;

    private readonly AlbumRepository _repository;
    private List<Album> _all = new();

    // Filtros ativos (clicar de novo no mesmo item remove o filtro).
    private int? _year;
    private MediaFormat? _format;
    private string? _artist;

    [ObservableProperty] private int totalItems;
    [ObservableProperty] private int vinylCount;
    [ObservableProperty] private int cdCount;
    [ObservableProperty] private int cassetteCount;
    [ObservableProperty] private string totalValueText = "—";
    [ObservableProperty] private string averageTicketText = "—";
    [ObservableProperty] private string mostExpensiveTitle = "—";
    [ObservableProperty] private string mostExpensiveDetail = "";
    [ObservableProperty] private Geometry? yearLine;
    [ObservableProperty] private Geometry? yearArea;

    [ObservableProperty] private bool hasFilters;
    [ObservableProperty] private string scopeText = "Toda a coleção";
    [ObservableProperty] private string albumsTitle = "Destaques da coleção";
    [ObservableProperty] private string moreText = "";
    [ObservableProperty] private bool hasAlbums;

    public ObservableCollection<BarItem> TopArtists { get; } = new();
    public ObservableCollection<DonutSlice> Formats { get; } = new();
    public ObservableCollection<YearPoint> YearPoints { get; } = new();
    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();
    public ObservableCollection<Album> Matches { get; } = new();

    public DashboardViewModel(AlbumRepository repository)
    {
        _repository = repository;
        Refresh();
    }

    /// <summary>Relê o banco (a coleção mudou) e recalcula tudo respeitando os filtros ativos.</summary>
    public void Refresh()
    {
        _all = _repository.GetAll().ToList();
        Recalculate();
    }

    // ---------- Filtros por clique ----------

    [RelayCommand]
    private void ToggleYear(string? label)
    {
        if (!int.TryParse(label, out var year))
            return;
        _year = _year == year ? null : year;
        Recalculate();
    }

    [RelayCommand]
    private void ToggleFormat(MediaFormat? format)
    {
        if (format is null)
            return;
        _format = _format == format ? null : format;
        Recalculate();
    }

    [RelayCommand]
    private void ToggleArtist(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        _artist = string.Equals(_artist, name, StringComparison.OrdinalIgnoreCase) ? null : name;
        Recalculate();
    }

    [RelayCommand]
    private void RemoveFilter(FilterChip? chip)
    {
        switch (chip?.Kind)
        {
            case "year": _year = null; break;
            case "format": _format = null; break;
            case "artist": _artist = null; break;
        }
        Recalculate();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _year = null;
        _format = null;
        _artist = null;
        Recalculate();
    }

    /// <summary>Álbuns que passam pelos filtros escolhidos. Cada gráfico ignora o filtro da própria dimensão
    /// (senão ele mostraria só a barra clicada) e apenas destaca a seleção.</summary>
    private List<Album> Filtered(bool byYear = true, bool byFormat = true, bool byArtist = true) =>
        _all.Where(a =>
                (!byYear || _year is not { } y || a.PurchaseYear == y) &&
                (!byFormat || _format is not { } f || a.Format == f) &&
                (!byArtist || _artist is not { } n || string.Equals(a.Artist.Trim(), n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private void Recalculate()
    {
        // Indicadores e lista de álbuns: todos os filtros juntos.
        var scoped = Filtered();
        var data = StatsService.Compute(scoped, topArtists: 6);

        TotalItems = data.Total;
        VinylCount = data.Vinyl;
        CdCount = data.CD;
        CassetteCount = data.Cassette;
        TotalValueText = Fmt.Money(data.TotalValue);
        AverageTicketText = Fmt.Money(data.AverageTicket);

        if (data.MostExpensive is { } top)
        {
            MostExpensiveTitle = top.Title;
            MostExpensiveDetail = $"{top.Artist}  ·  {Fmt.Money(top.Price)}";
        }
        else
        {
            MostExpensiveTitle = "—";
            MostExpensiveDetail = "";
        }

        // Etiquetas dos filtros ativos
        ActiveFilters.Clear();
        if (_year is { } yearFilter) ActiveFilters.Add(new FilterChip("year", $"Compra em {yearFilter}"));
        if (_format is { } formatFilter) ActiveFilters.Add(new FilterChip("format", MediaFormatInfo.Label(formatFilter)));
        if (_artist is { } artistFilter) ActiveFilters.Add(new FilterChip("artist", artistFilter));
        HasFilters = ActiveFilters.Count > 0;
        ScopeText = HasFilters ? string.Join("  ·  ", ActiveFilters.Select(c => c.Text)) : "Toda a coleção";

        FillBars(StatsService.Compute(Filtered(byArtist: false), topArtists: 6).TopArtists);
        FillDonut(Filtered(byFormat: false));
        FillYearChart(StatsService.Compute(Filtered(byYear: false)).ByPurchaseYear);
        FillAlbums(scoped);
    }

    private void FillAlbums(List<Album> scoped)
    {
        const int limit = 24;

        // Sem filtro mostra os mais caros; com filtro, os álbuns que combinam (mais caros primeiro).
        var ordered = scoped.OrderByDescending(a => a.Price).ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

        Matches.Clear();
        foreach (var album in ordered.Take(limit))
            Matches.Add(album);

        HasAlbums = Matches.Count > 0;
        AlbumsTitle = HasFilters ? $"Álbuns selecionados ({scoped.Count})" : "Destaques da coleção";
        MoreText = scoped.Count > limit ? $"e mais {scoped.Count - limit} álbuns" : "";
    }

    private void FillBars(IReadOnlyList<NamedCount> source)
    {
        var list = source.ToList();

        // O artista selecionado precisa continuar visível mesmo se sair do top 6 por causa de outro filtro.
        if (_artist is { } selected && !list.Any(x => string.Equals(x.Name, selected, StringComparison.OrdinalIgnoreCase)))
        {
            var count = Filtered(byArtist: false).Count(a => string.Equals(a.Artist.Trim(), selected, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
                list.Add(new NamedCount(selected, count));
        }

        TopArtists.Clear();
        var max = list.Count == 0 ? 1 : list.Max(x => x.Count);
        foreach (var item in list)
        {
            var isSelected = _artist is { } name && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase);
            TopArtists.Add(new BarItem(item.Name, item.Count, max, item.Count.ToString(),
                isSelected, _artist is null || isSelected ? 1 : 0.4));
        }
    }

    // ---------- Rosca de formatos ----------

    private void FillDonut(List<Album> albums)
    {
        Formats.Clear();
        var total = albums.Count;
        if (total == 0)
            return;

        var parts = new[]
        {
            (Format: MediaFormat.Vinyl, Count: albums.Count(a => a.Format == MediaFormat.Vinyl)),
            (Format: MediaFormat.CD, Count: albums.Count(a => a.Format == MediaFormat.CD)),
            (Format: MediaFormat.Cassette, Count: albums.Count(a => a.Format == MediaFormat.Cassette))
        }.Where(p => p.Count > 0).ToList();

        const double cx = 110, cy = 110, outer = 104, inner = 68;
        var gap = parts.Count > 1 ? 2.0 : 0.0;   // pequeno espaço entre as fatias
        var angle = 0.0;

        foreach (var part in parts)
        {
            var sweep = 360.0 * part.Count / total;
            Formats.Add(new DonutSlice
            {
                Data = BuildSlice(cx, cy, outer, inner, angle + gap / 2, Math.Max(sweep - gap, 1)),
                Format = part.Format,
                Label = MediaFormatInfo.Label(part.Format),
                Count = part.Count,
                PercentText = $"{100.0 * part.Count / total:0}%",
                IsSelected = _format == part.Format,
                Dim = _format is null || _format == part.Format ? 1 : 0.35
            });
            angle += sweep;
        }
    }

    /// <summary>Fatia de rosca. Ângulos em graus, a partir do topo, no sentido horário.</summary>
    private static Geometry BuildSlice(double cx, double cy, double outer, double inner, double start, double sweep)
    {
        sweep = Math.Min(sweep, 359.99);
        var end = start + sweep;
        var large = sweep > 180;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointAt(cx, cy, outer, start), true, true);
            ctx.ArcTo(PointAt(cx, cy, outer, end), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, false);
            ctx.LineTo(PointAt(cx, cy, inner, end), true, false);
            ctx.ArcTo(PointAt(cx, cy, inner, start), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointAt(double cx, double cy, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(cx + radius * Math.Sin(radians), cy - radius * Math.Cos(radians));
    }

    // ---------- Gráfico de linha: aquisições por ano ----------

    private void FillYearChart(IReadOnlyList<NamedCount> years)
    {
        YearPoints.Clear();
        YearLine = null;
        YearArea = null;

        if (years.Count == 0)
            return;

        var max = Math.Max(years.Max(y => y.Count), 1);
        var points = new List<Point>();

        for (var i = 0; i < years.Count; i++)
        {
            var x = years.Count == 1
                ? (PlotLeft + PlotRight) / 2
                : PlotLeft + i * (PlotRight - PlotLeft) / (years.Count - 1);
            var y = PlotBottom - years[i].Count / (double)max * (PlotBottom - PlotTop);
            points.Add(new Point(x, y));

            YearPoints.Add(new YearPoint
            {
                Label = years[i].Name,
                ValueText = years[i].Count.ToString(),
                DotLeft = x - 13,
                DotTop = y - 13,
                ValueLeft = x - 20,
                ValueTop = y - 28,
                LabelLeft = x - 20,
                LabelTop = LabelTop - 4,
                IsSelected = _year.HasValue && years[i].Name == _year.Value.ToString(),
                Dim = _year is null || years[i].Name == _year.Value.ToString() ? 1 : 0.4
            });
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            if (points.Count > 1)
                ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, PlotBottom), true, true);
            ctx.PolyLineTo(points, true, false);
            ctx.LineTo(new Point(points[^1].X, PlotBottom), true, false);
        }
        area.Freeze();

        YearLine = line;
        YearArea = area;
    }
}
