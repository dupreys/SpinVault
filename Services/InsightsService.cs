using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>Um item de ranking de gasto (ano, mídia ou artista).</summary>
public sealed record SpendItem(string Label, decimal Total, int Count);

/// <summary>Álbum que fez aniversário de lançamento neste mês.</summary>
public sealed record Anniversary(Album Album, int YearsAgo, int Day, bool IsToday)
{
    /// <summary>Aniversário "redondo" (5, 10, 15… anos).</summary>
    public bool IsRound => YearsAgo % 5 == 0;
    public string YearsText => YearsAgo == 1 ? "há 1 ano" : $"há {YearsAgo} anos";
}

public sealed class InsightsData
{
    public DateTime Today { get; init; }
    public int Count { get; init; }
    public decimal TotalValue { get; init; }
    public decimal AverageTicket { get; init; }
    public int Vinyl { get; init; }
    public int CD { get; init; }
    public int Cassette { get; init; }

    /// <summary>Gasto por ano de compra, do mais antigo ao mais recente (itens sem ano ficam num grupo "Sem ano" no fim).</summary>
    public IReadOnlyList<SpendItem> SpendByYear { get; init; } = Array.Empty<SpendItem>();

    /// <summary>Gasto por mídia, do maior para o menor.</summary>
    public IReadOnlyList<SpendItem> SpendByMedia { get; init; } = Array.Empty<SpendItem>();

    /// <summary>Artistas em que mais se gastou (só quem tem valor maior que zero).</summary>
    public IReadOnlyList<SpendItem> SpendByArtist { get; init; } = Array.Empty<SpendItem>();

    /// <summary>Artistas com mais álbuns (Total = quanto custaram juntos).</summary>
    public IReadOnlyList<SpendItem> TopArtistsByCount { get; init; } = Array.Empty<SpendItem>();

    /// <summary>Lançamentos do mês atual: "este mês há X anos".</summary>
    public IReadOnlyList<Anniversary> Anniversaries { get; init; } = Array.Empty<Anniversary>();

    /// <summary>Nome do mês atual em português ("novembro").</summary>
    public string MonthName { get; init; } = "";
}

/// <summary>Valor da coleção, rankings e aniversários de lançamento.</summary>
public static class InsightsService
{
    public const string NoYearLabel = "Sem ano";

    public static InsightsData Compute(IReadOnlyList<Album> albums, DateTime today, int top = 10)
    {
        var total = albums.Sum(a => a.Price);

        var byYear = albums.Where(a => a.PurchaseYear.HasValue)
            .GroupBy(a => a.PurchaseYear!.Value).OrderBy(g => g.Key)
            .Select(g => new SpendItem(g.Key.ToString(), g.Sum(a => a.Price), g.Count()))
            .ToList();
        var noYear = albums.Where(a => !a.PurchaseYear.HasValue).ToList();
        if (noYear.Count > 0)
            byYear.Add(new SpendItem(NoYearLabel, noYear.Sum(a => a.Price), noYear.Count));

        var byMedia = albums.GroupBy(a => a.Format)
            .Select(g => new SpendItem(MediaFormatInfo.Label(g.Key), g.Sum(a => a.Price), g.Count()))
            .OrderByDescending(x => x.Total).ThenByDescending(x => x.Count).ToList();

        var artistGroups = albums.GroupBy(a => a.Artist.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpendItem(g.Key, g.Sum(a => a.Price), g.Count())).ToList();

        var bySpend = artistGroups.Where(x => x.Total > 0)
            .OrderByDescending(x => x.Total).ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(top).ToList();

        var byCount = artistGroups
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.Total).ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(top).ToList();

        return new InsightsData
        {
            Today = today,
            Count = albums.Count,
            TotalValue = total,
            AverageTicket = albums.Count > 0 ? total / albums.Count : 0m,
            Vinyl = albums.Count(a => a.Format == MediaFormat.Vinyl),
            CD = albums.Count(a => a.Format == MediaFormat.CD),
            Cassette = albums.Count(a => a.Format == MediaFormat.Cassette),
            SpendByYear = byYear,
            SpendByMedia = byMedia,
            SpendByArtist = bySpend,
            TopArtistsByCount = byCount,
            Anniversaries = FindAnniversaries(albums, today),
            MonthName = Fmt.PtBr.DateTimeFormat.GetMonthName(today.Month)
        };
    }

    /// <summary>
    /// Álbuns lançados neste mês em anos anteriores. Datas 01/01 são tratadas como "só o ano"
    /// (é assim que o app guarda quando o dia não é conhecido) e por isso ficam de fora.
    /// </summary>
    public static IReadOnlyList<Anniversary> FindAnniversaries(IEnumerable<Album> albums, DateTime today) =>
        albums.Where(a => a.ReleaseDate is { } d && d.Month == today.Month && !(d.Month == 1 && d.Day == 1) && d.Year < today.Year)
            .Select(a =>
            {
                var d = a.ReleaseDate!.Value;
                return new Anniversary(a, today.Year - d.Year, d.Day, d.Day == today.Day);
            })
            .OrderBy(x => x.Day).ThenBy(x => x.Album.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Album.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
