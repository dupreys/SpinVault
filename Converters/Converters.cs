using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColecaoMidias.Models;
using ColecaoMidias.Services;

namespace ColecaoMidias.Converters;

/// <summary>Carrega e guarda em cache as capas (pode ser chamado fora da thread da interface).</summary>
public static class CoverImageLoader
{
    // Cada capa é decodificada uma única vez (a galeria recria cartões ao filtrar/recarregar).
    private static readonly Dictionary<string, BitmapImage> Cache = new();
    private static readonly Dictionary<string, BitmapImage> ByName = new();
    private static readonly object CacheLock = new();

    /// <summary>Devolve a capa se ela já estiver na memória (instantâneo, sem tocar no disco); senão null.</summary>
    public static ImageSource? TryGetCached(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        lock (CacheLock)
            return ByName.TryGetValue(name, out var image) ? image : null;
    }

    /// <summary>Carrega em segundo plano as capas que ainda não estão na memória.</summary>
    public static void Preload(IEnumerable<string?> names)
    {
        var pending = names.Where(n => !string.IsNullOrWhiteSpace(n) && TryGetCached(n) is null).Distinct().ToList();
        if (pending.Count == 0)
            return;

        Task.Run(() => Parallel.ForEach(pending, new ParallelOptions { MaxDegreeOfParallelism = 3 }, name => Load(name)));
    }

    public static ImageSource? Load(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var path = Path.IsPathRooted(text) ? text : CoverService.Resolve(text);
        if (path is null || !File.Exists(path))
            return null;

        var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;          // não trava o arquivo no disco
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = 360;                          // suficiente para cartão e painel; economiza memória
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();                                        // congelada: pode ser usada pela interface vinda de outra thread
            lock (CacheLock)
            {
                Cache[key] = image;
                ByName[text] = image;
            }
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Nome do arquivo (ou caminho completo) da capa → imagem. Devolve null se não existir.</summary>
public sealed class CoverImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        CoverImageLoader.Load(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Permite ligar RadioButtons a um enum: ConverterParameter=Vinyl, CD ou Cassette.</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null &&
        string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}

/// <summary>MediaFormat → pincel do tema (laranja vinil, azul CD, roxo cassete).</summary>
public sealed class FormatBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MediaFormat format)
            return null;

        return Application.Current.TryFindResource(MediaFormatInfo.BrushKey(format)) as Brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Liga RadioButtons a um texto-chave (ex.: página "dashboard", filtro "vinyl").
/// ConverterParameter = a chave que aquele botão representa.
/// </summary>
public sealed class KeyMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter?.ToString() : Binding.DoNothing;
}

/// <summary>Texto vazio → visível (usado na dica "Buscar…"); com texto → oculto.</summary>
public sealed class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → oculto, false → visível (o contrário do BooleanToVisibilityConverter).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
