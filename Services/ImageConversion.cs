using System.Windows.Media.Imaging;

namespace ColecaoMidias.Services;

/// <summary>Converte imagens em formatos menos comuns (GIF, BMP, WebP) para PNG.</summary>
public static class ImageConversion
{
    /// <summary>Devolve o PNG ou null se o Windows não conseguir abrir a imagem (ex.: WebP sem o codec instalado).</summary>
    public static byte[]? ToPng(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));

            using var output = new MemoryStream();
            encoder.Save(output);
            return output.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
