using System.Globalization;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>
/// Lê a aba "Base De Dados" da planilha de coleção:
/// A Album | B Artista | C Midia | D Lançamento | E Ano de Compra | F Valor (R$)
/// e, nas planilhas exportadas pela v1.1: G Gênero | H Listas | I Nota | J Conservação (mídia) | K Conservação (capa) | L Local | M Observações | N Emprestado para | O Data do empréstimo
/// (cabeçalho na linha 2, dados a partir da linha 3).
/// </summary>
public sealed class ExcelImporter
{
    private const string SheetName = "Base De Dados";

    public IReadOnlyList<Album> Read(string path, List<string> warnings)
    {
        // FileShare.ReadWrite permite importar mesmo com a planilha aberta no Excel.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Read(stream, warnings);
    }

    public IReadOnlyList<Album> Read(Stream stream, List<string> warnings)
    {
        var rows = XlsxReader.ReadSheet(stream, SheetName);

        // Procura a linha de cabeçalho (a que tem "Album" na coluna A); no seu arquivo é a linha 2.
        var header = rows.FirstOrDefault(r =>
            r.Number <= 10 && r.Get("A").Equals("Album", StringComparison.OrdinalIgnoreCase));
        var firstDataRow = (header?.Number ?? 2) + 1;

        var albums = new List<Album>();

        foreach (var row in rows.Where(r => r.Number >= firstDataRow))
        {
            var title = row.Get("A");
            var artist = row.Get("B");

            if (title.Length == 0 && artist.Length == 0)
                continue; // linha em branco

            if (title.Length == 0 || artist.Length == 0)
            {
                warnings.Add(Loc.F("Linha {0}: álbum ou artista vazio — ignorada.", row.Number));
                continue;
            }

            var formatText = row.Get("C");
            if (!MediaFormatInfo.TryParse(formatText, out var format))
            {
                warnings.Add(Loc.F("Linha {0} ({1}): mídia \"{2}\" não reconhecida — ignorada.", row.Number, title, formatText));
                continue;
            }

            albums.Add(new Album
            {
                Title = title,
                Artist = artist,
                Format = format,
                ReleaseDate = ReadDate(row.Get("D")),
                PurchaseYear = ReadYear(row.Get("E")),
                Price = ReadPrice(row.Get("F")),

                // Colunas G a M existem nas planilhas exportadas pela v1.1; nas antigas ficam vazias.
                Genre = NullIfEmpty(row.Get("G")),
                Tags = TagUtil.Join(TagUtil.Split(row.Get("H"))),
                Rating = ReadRating(row.Get("I")),
                MediaCondition = Conditions.Parse(row.Get("J")),
                CoverCondition = Conditions.Parse(row.Get("K")),
                PurchasePlace = NullIfEmpty(row.Get("L")),
                Notes = NullIfEmpty(row.Get("M")),

                // Colunas N e O (v1.2): empréstimo.
                LoanedTo = NullIfEmpty(row.Get("N")),
                LoanedOn = NullIfEmpty(row.Get("N")) is null ? null : ReadDate(row.Get("O")) ?? DateTime.Today
            });
        }

        return albums;
    }

    private static DateTime? ReadDate(string text)
    {
        if (text.Length == 0)
            return null;

        // Data "de verdade" no Excel chega como número de série (ex.: 32266 = 07/03/1988).
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            if (number is >= 1900 and <= 2100)
                return new DateTime((int)number, 1, 1);      // só o ano digitado
            if (number is > 2100 and < 2958466)
                return DateTime.FromOADate(number).Date;
            return null;
        }

        // Ex.: a célula de "Relayer" está como texto "29/11/1974"
        return Fmt.TryParseDate(text, out var date) ? date : null;
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    private static int ReadRating(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? (int)Math.Clamp(Math.Round(n), 0, 5) : 0;

    private static int? ReadYear(string text)
    {
        if (text.Length == 0)
            return null;

        var ok = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number);
        var year = ok ? (int)number : 0;
        return year is >= 1900 and <= 2100 ? year : null;
    }

    private static decimal ReadPrice(string text)
    {
        if (text.Length == 0)
            return 0m;

        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var number))
            return Math.Round(number, 2);

        return Fmt.TryParseMoney(text, out var value) ? value : 0m;
    }
}
