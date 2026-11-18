using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>Como a coleção é ordenada e como é agrupada em seções.</summary>
public static class CollectionOrdering
{
    // ----- Chaves de ordenação -----
    public const string SortArtist = "artist";
    public const string SortTitle = "title";
    public const string SortOldest = "oldest";
    public const string SortNewest = "newest";
    public const string SortPrice = "price";
    public const string SortRating = "rating";
    public const string SortAdded = "added";

    // ----- Chaves de agrupamento -----
    public const string GroupNone = "none";
    public const string GroupGenre = "genre";
    public const string GroupDecade = "decade";
    public const string GroupArtist = "artist";

    // Propriedades (e não campos) para que os nomes sejam traduzidos no idioma já escolhido, e não no da primeira leitura.
    public static (string Key, string Label)[] SortOptions => new[]
    {
        (SortArtist, Loc.T("Artista (A–Z)")),
        (SortTitle, Loc.T("Título (A–Z)")),
        (SortOldest, Loc.T("Lançamento: mais antigos")),
        (SortNewest, Loc.T("Lançamento: mais novos")),
        (SortPrice, Loc.T("Maior valor")),
        (SortRating, Loc.T("Melhor nota")),
        (SortAdded, Loc.T("Adicionados por último")),
    };

    public static (string Key, string Label)[] GroupOptions => new[]
    {
        (GroupNone, Loc.T("Sem agrupar")),
        (GroupGenre, Loc.T("Gênero")),
        (GroupDecade, Loc.T("Década")),
        (GroupArtist, Loc.T("Artista")),
    };

    public static string NormalizeSort(string? key) =>
        SortOptions.Any(o => o.Key == key) ? key! : SortArtist;

    public static string NormalizeGroup(string? key) =>
        GroupOptions.Any(o => o.Key == key) ? key! : GroupNone;

    public static string NoGenre => Loc.T("Sem gênero");
    public static string NoDate => Loc.T("Sem data de lançamento");

    /// <summary>Nome da seção a que o álbum pertence ("Rock", "Anos 80", "Yes"); null quando não há agrupamento.</summary>
    public static string? GroupLabel(Album album, string groupKey) => groupKey switch
    {
        GroupGenre => album.HasGenre ? album.Genre!.Trim() : NoGenre,
        GroupDecade => album.Decade is { } d ? Decades.Label(d) : NoDate,
        GroupArtist => string.IsNullOrWhiteSpace(album.Artist) ? Loc.T("Sem artista") : album.Artist.Trim(),
        _ => null
    };

    /// <summary>
    /// Ordena a lista. Com agrupamento, as seções vêm em ordem natural (gêneros e artistas de A a Z,
    /// décadas da mais antiga para a mais nova, "sem ..." por último) e a ordenação escolhida vale dentro de cada seção.
    /// </summary>
    public static List<Album> Sort(IEnumerable<Album> albums, string sortKey, string groupKey)
    {
        sortKey = NormalizeSort(sortKey);
        groupKey = NormalizeGroup(groupKey);
        var text = StringComparer.CurrentCultureIgnoreCase;

        IOrderedEnumerable<Album> ordered = groupKey switch
        {
            GroupGenre => albums.OrderBy(a => a.HasGenre ? 0 : 1).ThenBy(a => a.HasGenre ? a.Genre!.Trim() : "", text),
            GroupDecade => albums.OrderBy(a => a.Decade is null ? 1 : 0).ThenBy(a => a.Decade ?? 0),
            GroupArtist => albums.OrderBy(a => a.Artist.Trim(), text),
            _ => albums.OrderBy(_ => 0)
        };

        var result = sortKey switch
        {
            SortTitle => ordered.ThenBy(a => a.Title, text).ThenBy(a => a.Artist, text),
            SortOldest => ordered.ThenBy(a => a.ReleaseDate is null ? 1 : 0).ThenBy(a => a.ReleaseDate).ThenBy(a => a.Artist, text).ThenBy(a => a.Title, text),
            SortNewest => ordered.ThenBy(a => a.ReleaseDate is null ? 1 : 0).ThenByDescending(a => a.ReleaseDate).ThenBy(a => a.Artist, text).ThenBy(a => a.Title, text),
            SortPrice => ordered.ThenByDescending(a => a.Price).ThenBy(a => a.Artist, text).ThenBy(a => a.Title, text),
            SortRating => ordered.ThenByDescending(a => a.Rating).ThenBy(a => a.Artist, text).ThenBy(a => a.Title, text),
            SortAdded => ordered.ThenByDescending(a => a.Id),
            _ => ordered.ThenBy(a => a.Artist, text).ThenBy(a => a.ReleaseDate).ThenBy(a => a.Title, text),
        };

        return result.ToList();
    }
}
