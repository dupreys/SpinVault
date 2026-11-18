using System.Globalization;
#if !SPINVAULT_MOBILE
using System.Windows;
#endif

namespace ColecaoMidias.Services;

/// <summary>
/// Idioma do app (Português do Brasil ou English). Os textos do programa são escritos em português e
/// traduzidos na hora de mostrar: <c>Loc.T("Salvar")</c> devolve "Save" quando o idioma é inglês.
/// Textos com valores usam <c>Loc.F("{0} álbuns", n)</c>. O idioma só muda ao abrir o app (a troca reinicia).
/// </summary>
public static class Loc
{
    public const string Portuguese = "pt";
    public const string English = "en";

    private static readonly CultureInfo PtCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo EnCulture = CreateEnglishCulture();

    public static string Code { get; private set; } = Portuguese;
    public static bool IsEnglish => Code == English;
    public static CultureInfo Culture => IsEnglish ? EnCulture : PtCulture;

    /// <summary>Lê o idioma salvo; se o usuário nunca escolheu, segue o idioma do Windows.</summary>
    public static void Init(string? saved)
    {
        Code = Normalize(saved) ?? Detect();
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        Thread.CurrentThread.CurrentCulture = Culture;
        Thread.CurrentThread.CurrentUICulture = Culture;
    }

    public static string? Normalize(string? code)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        if (c.StartsWith("pt")) return Portuguese;
        if (c.StartsWith("en")) return English;
        return null;
    }

    private static string Detect() =>
        Normalize(CultureInfo.InstalledUICulture.Name)
        ?? Normalize(CultureInfo.CurrentUICulture.Name)   // Android: o idioma do aparelho vem daqui
        ?? English;

    /// <summary>Troca o idioma sem reiniciar (usado no app Android, que recria as telas).</summary>
    public static void Set(string code) => Init(code);

    /// <summary>Traduz um texto escrito em português (devolve o próprio texto se não houver tradução).</summary>
    public static string T(string pt)
    {
        if (!IsEnglish || string.IsNullOrEmpty(pt))
            return pt;
        return Translations.ToEnglish(pt);
    }

    /// <summary>Traduz o modelo e preenche os valores: <c>F("{0} de {1} itens", a, b)</c>.</summary>
    public static string F(string ptFormat, params object?[] args) =>
        string.Format(Culture, T(ptFormat), args);

    /// <summary>Singular/plural: <c>N(n, "1 álbum", "{0} álbuns")</c>.</summary>
    public static string N(int count, string one, string many) =>
        count == 1 ? T(one) : F(many, count);

#if !SPINVAULT_MOBILE
    /// <summary>Reabre o app (usado depois de trocar o idioma).</summary>
    public static void Restart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return;   // não conseguiu reabrir: o idioma vale na próxima vez que o usuário abrir o app
        }
        Application.Current.Shutdown();
    }
#endif

    private static CultureInfo CreateEnglishCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.CurrencySymbol = "R$";
        culture.NumberFormat.CurrencyPositivePattern = 2;   // "R$ 1,234.56"
        culture.NumberFormat.CurrencyNegativePattern = 9;   // "-R$ 1,234.56"
        return culture;
    }
}
