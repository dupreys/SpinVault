using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>
/// Lê arquivos CSV: o exportado por este app, o exportado pelo Discogs ("Coleção → Exportar")
/// ou qualquer planilha CSV com colunas parecidas (Álbum, Artista, Mídia…).
/// Descobre sozinho o separador (; , ou tab), a codificação e o formato.
/// </summary>
public sealed class CsvImporter
{
    public enum CsvKind { Generic, Discogs }

    public CsvKind LastKind { get; private set; } = CsvKind.Generic;

    public IReadOnlyList<Album> Read(string path, List<string> warnings)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Read(stream, warnings);
    }

    public IReadOnlyList<Album> Read(Stream stream, List<string> warnings)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var text = Decode(memory.ToArray());

        // Tenta o separador mais provável primeiro; fica com o que produzir uma linha de cabeçalho reconhecível.
        List<List<string>> rows = new();
        var headerIndex = -1;
        foreach (var delimiter in DelimitersByLikelihood(text))
        {
            rows = Parse(text, delimiter);
            headerIndex = rows.FindIndex(r => KnownColumns(r) >= 2);
            if (headerIndex >= 0)
                break;
        }

        if (rows.Count == 0)
            throw new InvalidDataException("O arquivo CSV está vazio.");

        if (headerIndex < 0)
            throw new InvalidDataException(
                "Não reconheci as colunas do CSV. O arquivo precisa ter, no mínimo, as colunas Álbum (ou Title) e Artista (ou Artist).");

        var header = rows[headerIndex].Select(Norm).ToList();
        var col = new Dictionary<string, int>();
        for (var i = 0; i < header.Count; i++)
            foreach (var (field, aliases) in Aliases)
                if (!col.ContainsKey(field) && aliases.Contains(header[i]))
                    col[field] = i;

        if (!col.ContainsKey("title") || !col.ContainsKey("artist"))
            throw new InvalidDataException("O CSV precisa ter as colunas Álbum (ou Title) e Artista (ou Artist).");

        LastKind = header.Contains("release_id") || header.Contains("catalog#") || header.Contains("collectionfolder")
            ? CsvKind.Discogs : CsvKind.Generic;
        var discogs = LastKind == CsvKind.Discogs;

        string Get(List<string> row, string field) =>
            col.TryGetValue(field, out var i) && i < row.Count ? row[i].Trim() : "";

        var albums = new List<Album>();
        for (var r = headerIndex + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var lineNumber = r + 1;
            if (row.All(c => string.IsNullOrWhiteSpace(c)))
                continue;

            var title = Unprotect(Get(row, "title"));
            var artist = Unprotect(Get(row, "artist"));
            if (discogs)
                artist = CleanDiscogsArtist(artist);

            if (title.Length == 0 || artist.Length == 0)
            {
                warnings.Add($"Linha {lineNumber}: álbum ou artista vazio — ignorada.");
                continue;
            }

            var formatText = Get(row, "format");
            if (!TryFormat(formatText, out var format))
            {
                warnings.Add($"Linha {lineNumber} ({title}): mídia \"{formatText}\" não reconhecida (só vinil, CD e cassete) — ignorada.");
                continue;
            }

            var tags = TagUtil.Split(Get(row, "tags")).ToList();
            if (discogs)
            {
                // A pasta do Discogs vira uma lista (menos as pastas padrão).
                var folder = Get(row, "folder");
                if (folder.Length > 0 && !folder.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)
                    && !folder.Equals("All", StringComparison.OrdinalIgnoreCase)
                    && !folder.Equals("Sem categoria", StringComparison.OrdinalIgnoreCase))
                    tags.Add(folder);
            }

            var album = new Album
            {
                Title = title,
                Artist = artist,
                Format = format,
                ReleaseDate = ReadDate(Get(row, "release")),
                PurchaseYear = ReadYear(Get(row, "purchaseyear")),
                Price = Fmt.TryParseMoney(Get(row, "price"), out var price) && price >= 0 ? Math.Round(price, 2) : 0m,
                Genre = NullIfEmpty(Unprotect(Get(row, "genre"))),
                Tags = TagUtil.Join(tags),
                Rating = ReadRating(Get(row, "rating")),
                MediaCondition = Conditions.Parse(Get(row, "mediacondition")),
                CoverCondition = Conditions.Parse(Get(row, "covercondition")),
                PurchasePlace = NullIfEmpty(Unprotect(Get(row, "place"))),
                Notes = NullIfEmpty(Unprotect(Get(row, "notes")))
            };
            albums.Add(album);
        }

        return albums;
    }

    // ---------- Colunas reconhecidas (já sem acento, minúsculas) ----------

    private static readonly (string Field, HashSet<string> Aliases)[] Aliases =
    {
        ("title",          new() { "album", "titulo", "title", "nome", "disco", "nome do album" }),
        ("artist",         new() { "artista", "artist", "banda", "interprete" }),
        ("format",         new() { "midia", "formato", "format", "media", "tipo", "suporte" }),
        ("release",        new() { "lancamento", "data de lancamento", "ano de lancamento", "release date", "released", "release" }),
        ("purchaseyear",   new() { "ano de compra", "comprado em", "ano da compra", "purchase year" }),
        ("price",          new() { "valor", "valor (r$)", "preco", "preco (r$)", "price", "valor pago" }),
        ("genre",          new() { "genero", "genre", "estilo" }),
        ("tags",           new() { "listas", "lista", "tags", "etiquetas", "colecoes" }),
        ("rating",         new() { "nota", "avaliacao", "rating", "estrelas" }),
        ("mediacondition", new() { "conservacao (midia)", "conservacao da midia", "estado da midia", "conservacao",
                                   "collection media condition", "media condition" }),
        ("covercondition", new() { "conservacao (capa)", "conservacao da capa", "estado da capa",
                                   "collection sleeve condition", "sleeve condition", "cover condition" }),
        ("place",          new() { "local de compra", "onde comprou", "loja", "comprado em (local)", "store", "purchase place" }),
        ("notes",          new() { "observacoes", "observacao", "notas pessoais", "notes", "collection notes", "obs" }),
        ("folder",         new() { "collectionfolder", "pasta" }),
    };

    private static int KnownColumns(List<string> row)
    {
        var norm = row.Select(Norm).ToList();
        var known = 0;
        foreach (var (_, aliases) in Aliases)
            if (norm.Any(aliases.Contains))
                known++;
        return known;
    }

    // ---------- Valores ----------

    private static bool TryFormat(string text, out MediaFormat format)
    {
        if (MediaFormatInfo.TryParse(text, out format))
            return true;

        // Discogs descreve vinil por tamanho/rotação: 12", 7", 33 ⅓ RPM, 45 RPM…
        if (Regex.IsMatch(text, "(^|[^0-9])(7|10|12)\\s*\"") || Regex.IsMatch(text, "\\b(33|45|78)\\b.*rpm", RegexOptions.IgnoreCase)
            || text.Contains("shellac", StringComparison.OrdinalIgnoreCase))
        {
            format = MediaFormat.Vinyl;
            return true;
        }

        format = default;
        return false;
    }

    /// <summary>"Pink Floyd (2)" → "Pink Floyd"; "Beatles, The" → "The Beatles".</summary>
    internal static string CleanDiscogsArtist(string name)
    {
        name = Regex.Replace(name, @"\s*\(\d+\)\s*$", "").Trim();
        var m = Regex.Match(name, @"^(.*),\s*(The|A|An)$", RegexOptions.IgnoreCase);
        return m.Success ? $"{m.Groups[2].Value} {m.Groups[1].Value.Trim()}" : name;
    }

    private static DateTime? ReadDate(string text)
    {
        if (text.Length == 0)
            return null;
        if (Fmt.TryParseDate(text, out var date))
            return date;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var any) && any.Year is >= 1900 and <= 2100)
            return any.Date;
        // "1985-06" ou "1985-00-00"
        var m = Regex.Match(text, @"^(\d{4})");
        return m.Success && int.Parse(m.Groups[1].Value) is >= 1900 and <= 2100 ? new DateTime(int.Parse(m.Groups[1].Value), 1, 1) : null;
    }

    private static int? ReadYear(string text) =>
        int.TryParse(text.Trim(), out var year) && year is >= 1900 and <= 2100 ? year : null;

    private static int ReadRating(string text)
    {
        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return text.Count(c => c == '★');   // aceita "★★★★"
        return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 5);
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    /// <summary>O export do app prefixa ' em textos que começam com = + @ (proteção do Excel); aqui tira de volta.</summary>
    private static string Unprotect(string text) =>
        text.Length > 1 && text[0] == '\'' && text[1] is '=' or '+' or '@' ? text[1..] : text;

    private static string Norm(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim().TrimStart('﻿');
    }

    // ---------- Leitura do arquivo ----------

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Excel em português costuma salvar CSV em Windows-1252; Latin-1 cobre os acentos.
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Separadores em ordem de probabilidade: o que mais aparece (fora de aspas) nas primeiras linhas.</summary>
    internal static IReadOnlyList<char> DelimitersByLikelihood(string text)
    {
        var counts = new Dictionary<char, int> { [';'] = 0, [','] = 0, ['\t'] = 0 };
        var inQuotes = false;
        var lines = 0;

        foreach (var c in text)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (inQuotes) continue;
            if (c == '\n') { if (++lines >= 25) break; continue; }
            if (counts.ContainsKey(c))
                counts[c]++;
        }

        return counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    /// <summary>Leitor de CSV (RFC 4180): aspas, aspas duplas e quebras de linha dentro de campos.</summary>
    internal static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;

        void EndField() { row.Add(field.ToString()); field.Clear(); fieldStarted = false; }
        void EndRow()
        {
            EndField();
            rows.Add(row);
            row = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            if (c == '"' && !fieldStarted) { inQuotes = true; fieldStarted = true; }
            else if (c == delimiter) EndField();
            else if (c == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; EndRow(); }
            else if (c == '\n') EndRow();
            else { field.Append(c); fieldStarted = true; }
        }

        if (field.Length > 0 || row.Count > 0 || fieldStarted)
            EndRow();

        return rows;
    }
}
