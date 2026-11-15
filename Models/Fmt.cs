using System.Globalization;

namespace ColecaoMidias.Models;

/// <summary>Formatação e leitura de valores no padrão brasileiro (pt-BR).</summary>
public static class Fmt
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "yyyy-MM-dd"
    };

    public static string Money(decimal value) => value.ToString("C2", PtBr);

    public static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString("dd/MM/yyyy", PtBr) : "—";

    /// <summary>Aceita "40", "40,50", "R$ 1.234,56", "40.5".</summary>
    public static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = new string(text.Where(ch => char.IsDigit(ch) || ch == ',' || ch == '.' || ch == '-').ToArray());
        if (s.Length == 0)
            return false;

        if (s.Contains(','))
            s = s.Replace(".", "").Replace(',', '.');   // formato brasileiro
        else if (s.Count(c => c == '.') > 1)
            s = s.Replace(".", "");                      // 1.234.567

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Aceita "dd/mm/aaaa" ou apenas o ano ("1996" vira 01/01/1996).</summary>
    public static bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        var s = (text ?? "").Trim();
        if (s.Length == 0)
            return false;

        if (DateTime.TryParseExact(s, DateFormats, PtBr, DateTimeStyles.None, out date))
            return true;

        if (s.Length == 4 && int.TryParse(s, out var year) && year is >= 1900 and <= 2100)
        {
            date = new DateTime(year, 1, 1);
            return true;
        }

        return false;
    }
}
