using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

public sealed record NamedCount(string Name, int Count);

public sealed class DashboardData
{
    public int Total { get; init; }
    public int Vinyl { get; init; }
    public int CD { get; init; }
    public int Cassette { get; init; }
    public decimal TotalValue { get; init; }
    public decimal AverageTicket { get; init; }
    public Album? MostExpensive { get; init; }
    public IReadOnlyList<NamedCount> TopArtists { get; init; } = Array.Empty<NamedCount>();
    public IReadOnlyList<NamedCount> ByPurchaseYear { get; init; } = Array.Empty<NamedCount>();
}

/// <summary>Os cálculos que na planilha eram tabelas dinâmicas.</summary>
public static class StatsService
{
    public static DashboardData Compute(IReadOnlyList<Album> albums, int topArtists = 8)
    {
        var total = albums.Count;
        var totalValue = albums.Sum(a => a.Price);

        var artists = albums
            .GroupBy(a => a.Artist.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(topArtists)
            .ToList();

        var years = albums
            .Where(a => a.PurchaseYear.HasValue)
            .GroupBy(a => a.PurchaseYear!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new NamedCount(g.Key.ToString(), g.Count()))
            .ToList();

        return new DashboardData
        {
            Total = total,
            Vinyl = albums.Count(a => a.Format == MediaFormat.Vinyl),
            CD = albums.Count(a => a.Format == MediaFormat.CD),
            Cassette = albums.Count(a => a.Format == MediaFormat.Cassette),
            TotalValue = totalValue,
            AverageTicket = total > 0 ? totalValue / total : 0m,
            MostExpensive = albums.OrderByDescending(a => a.Price).FirstOrDefault(),
            TopArtists = artists,
            ByPurchaseYear = years
        };
    }
}
