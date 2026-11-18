using System.Globalization;
using ColecaoMidias.Services;

namespace ColecaoMidias.Models;

/// <summary>Formatação e leitura de valores (datas, dinheiro) no idioma escolhido: pt-BR ou English.</summary>
public static class Fmt
{
    /// <summary>Cultura do idioma atual (pt-BR ou en-US com R$).</summary>
    public static CultureInfo Current => Loc.Culture;

    private static readonly string[] PtDateFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "yyyy-MM-dd" };
    private static readonly string[] EnDateFormats = { "MM/dd/yyyy", "M/d/yyyy", "MM/dd/yy", "yyyy-MM-dd" };

    /// <summary>Formato mostrado ao usuário: 25/12/2026 em português, 12/25/2026 em inglês.</summary>
    public static string DatePattern => Loc.IsEnglish ? "MM/dd/yyyy" : "dd/MM/yyyy";

    /// <summary>"25 de dezembro de 2026" em português, "December 25, 2026" em inglês.</summary>
    public static string LongDate(DateTime date) =>
        Loc.IsEnglish ? date.ToString("MMMM d, yyyy", Current) : date.ToString("dd 'de' MMMM 'de' yyyy", Current);

    public static string Money(decimal value) => value.ToString("C2", Current);

    public static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString(DatePattern, Current) : "—";

    /// <summary>Aceita "40", "40,50", "R$ 1.234,56", "40.5".</summary>
    public static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = new string(text.Where(ch => char.IsDigit(ch) || ch == ',' || ch == '.' || ch == '-').ToArray());
        if (s.Length == 0)
            return false;

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Os dois aparecem: o que vem por último é o separador decimal ("1.234,56" ou "1,234.56").
            s = lastDot > lastComma ? s.Replace(",", "") : s.Replace(".", "").Replace(',', '.');
        }
        else if (lastComma >= 0)
        {
            // Só vírgula: decimal ("45,90"), exceto em inglês quando parece milhar ("1,234" ou "1,234,567").
            s = Loc.IsEnglish && System.Text.RegularExpressions.Regex.IsMatch(s, @"^-?\d{1,3}(,\d{3})+$")
                ? s.Replace(",", "")
                : s.Replace(',', '.');
        }
        else if (s.Count(c => c == '.') > 1)
        {
            s = s.Replace(".", "");                      // 1.234.567
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Aceita a data no formato do idioma (ou o outro) ou apenas o ano ("1996" vira 01/01/1996).</summary>
    public static bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        var s = (text ?? "").Trim();
        if (s.Length == 0)
            return false;

        // Tenta primeiro o formato do idioma atual e depois o outro (para ler arquivos de outro idioma).
        var first = Loc.IsEnglish ? EnDateFormats : PtDateFormats;
        var second = Loc.IsEnglish ? PtDateFormats : EnDateFormats;
        if (DateTime.TryParseExact(s, first, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateTime.TryParseExact(s, second, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (s.Length == 4 && int.TryParse(s, out var year) && year is >= 1900 and <= 2100)
        {
            date = new DateTime(year, 1, 1);
            return true;
        }

        return false;
    }
}
