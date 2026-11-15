using System.Globalization;
using System.Text;

namespace ColecaoMidias.Models;

/// <summary>Um grau de conservação (escala usada por colecionadores e pelo Discogs).</summary>
public sealed record ConditionGrade(string Code, string Label);

public static class Conditions
{
    public static readonly IReadOnlyList<ConditionGrade> All = new ConditionGrade[]
    {
        new("M",   "Novo (M)"),
        new("NM",  "Quase novo (NM)"),
        new("VG+", "Excelente (VG+)"),
        new("VG",  "Muito bom (VG)"),
        new("G+",  "Bom+ (G+)"),
        new("G",   "Bom (G)"),
        new("F",   "Regular (F)"),
        new("P",   "Ruim (P)")
    };

    /// <summary>"NM" → "Quase novo (NM)". Vazio quando não há grau.</summary>
    public static string Label(string? code) =>
        All.FirstOrDefault(c => c.Code == code)?.Label ?? "";

    /// <summary>
    /// Reconhece o grau em português, inglês (Discogs) ou pelo código:
    /// "Near Mint (NM or M-)", "Very Good Plus (VG+)", "Excelente", "vg+" …
    /// Devolve null quando não reconhece (ex.: "Generic", "Not Graded").
    /// </summary>
    public static string? Parse(string? text)
    {
        var t = Simplify(text);
        if (t.Length == 0)
            return null;

        // Código exato (M, NM, VG+, VG, G+, G, F, P)
        var upper = t.ToUpperInvariant();
        if (All.Any(g => g.Code == upper))
            return upper;

        // Ordem importa: os mais específicos primeiro.
        if (t.Contains("quase novo") || t.Contains("near mint") || t.Contains("(nm")) return "NM";
        if (t.Contains("very good plus") || t.Contains("(vg+)") || t.Contains("excelente")) return "VG+";
        if (t.Contains("very good") || t.Contains("(vg)") || t.Contains("muito bom")) return "VG";
        if (t.Contains("good plus") || t.Contains("(g+)") || t.Contains("bom+") || t.Contains("bom +")) return "G+";
        if (t.Contains("mint") || t.Contains("(m)") || t == "novo" || t.StartsWith("novo ") || t.Contains("lacrado")) return "M";
        if (t.Contains("good") || t.Contains("(g)") || t == "bom") return "G";
        if (t.Contains("fair") || t.Contains("(f)") || t.Contains("regular")) return "F";
        if (t.Contains("poor") || t.Contains("(p)") || t.Contains("ruim")) return "P";
        return null;
    }

    private static string Simplify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>Listas/etiquetas do usuário ("Favoritos", "Para ouvir"…), guardadas num único texto separado por ";".</summary>
public static class TagUtil
{
    public const string Favorites = "Favoritos";
    public const string ToListen = "Para ouvir";

    public static readonly string[] Defaults = { Favorites, ToListen };

    public static IReadOnlyList<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var part in text.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Clean(part);
            if (name.Length > 0 && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                list.Add(name);
        }
        return list;
    }

    public static string? Join(IEnumerable<string> tags)
    {
        var clean = tags.Select(Clean).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return clean.Count == 0 ? null : string.Join(";", clean);
    }

    /// <summary>Tira separadores e espaços repetidos.</summary>
    public static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        var cleaned = name.Replace(';', ' ').Replace(',', ' ').Replace('|', ' ');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public static class Decades
{
    /// <summary>1985 → 1980; sem ano → null.</summary>
    public static int? Of(DateTime? date) => date is { } d ? d.Year / 10 * 10 : null;

    /// <summary>1980 → "Anos 80"; 2000 → "Anos 2000".</summary>
    public static string Label(int decade) => decade >= 2000 ? $"Anos {decade}" : $"Anos {decade % 100:00}";
}
