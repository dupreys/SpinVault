using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>Um álbum que aparece mais de uma vez na coleção.</summary>
/// <param name="SameFormat">Pelo menos duas cópias na mesma mídia (provável cadastro repetido). Se for false, são mídias diferentes (ex.: vinil e CD), o que costuma ser proposital.</param>
public sealed record DuplicateGroup(string Title, string Artist, IReadOnlyList<Album> Items, bool SameFormat)
{
    public string Kind => SameFormat ? Loc.T("Mesma mídia repetida") : Loc.T("Em mídias diferentes");
}

/// <summary>Acha álbuns repetidos ignorando maiúsculas, acentos, pontuação e sufixos entre parênteses.</summary>
public static class DuplicateService
{
    public static IReadOnlyList<DuplicateGroup> Find(IEnumerable<Album> albums) =>
        albums
            .GroupBy(a => Normalize(a.Artist) + "|" + Normalize(a.Title))
            .Where(g => g.Key.Length > 1 && g.Count() > 1)
            .Select(g =>
            {
                var items = g.OrderBy(a => a.Format).ThenBy(a => a.PurchaseYear).ToList();
                var sameFormat = items.GroupBy(a => a.Format).Any(f => f.Count() > 1);
                return new DuplicateGroup(items[0].Title, items[0].Artist, items, sameFormat);
            })
            .OrderByDescending(g => g.SameFormat)          // repetições na mesma mídia primeiro
            .ThenBy(g => g.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // "Abbey Road (Remastered)" e "Abbey Road [Deluxe]" contam como "Abbey Road".
        var withoutBrackets = Regex.Replace(text, @"\s*[\(\[][^\)\]]*[\)\]]", "");
        if (withoutBrackets.Trim().Length == 0)
            withoutBrackets = text;

        var decomposed = withoutBrackets.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString();
    }
}
