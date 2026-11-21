using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

public sealed record CountItem(string Label, int Count);

/// <summary>Quantos álbuns há por década, por gênero e por mídia (os gráficos da tela Resumo).</summary>
public static class DistributionService
{
    /// <summary>Da década mais antiga para a mais nova; "sem data" por último.</summary>
    public static IReadOnlyList<CountItem> ByDecade(IEnumerable<Album> albums)
    {
        var list = albums.ToList();
        var result = list.Where(a => a.Decade is not null)
            .GroupBy(a => a.Decade!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new CountItem(Decades.Label(g.Key), g.Count()))
            .ToList();

        var without = list.Count(a => a.Decade is null);
        if (without > 0)
            result.Add(new CountItem(CollectionOrdering.NoDate, without));
        return result;
    }

    /// <summary>Os gêneros com mais álbuns; o que passa de <paramref name="top"/> vira "Outros". "Sem gênero" fica por último.</summary>
    public static IReadOnlyList<CountItem> ByGenre(IEnumerable<Album> albums, int top = 8)
    {
        var list = albums.ToList();
        var named = list.Where(a => a.HasGenre)
            .GroupBy(a => a.Genre!.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new CountItem(g.Key, g.Count()))
            .OrderByDescending(i => i.Count)
            .ThenBy(i => i.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var result = named.Take(top).ToList();
        var others = named.Skip(top).Sum(i => i.Count);
        if (others > 0)
            result.Add(new CountItem(Loc.T("Outros"), others));

        var without = list.Count(a => !a.HasGenre);
        if (without > 0)
            result.Add(new CountItem(CollectionOrdering.NoGenre, without));
        return result;
    }

    public static IReadOnlyList<CountItem> ByFormat(IEnumerable<Album> albums) =>
        albums.GroupBy(a => a.Format)
            .OrderBy(g => (int)g.Key)
            .Select(g => new CountItem(MediaFormatInfo.Label(g.Key), g.Count()))
            .ToList();
}
