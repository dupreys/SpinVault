using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace SpinVaultMobile;

/// <summary>Reduz as capas para um tamanho bom de galeria, deixando o app mais leve e rápido.</summary>
public static class ImageHelper
{
    private const float MaxSide = 700f;

    /// <summary>Devolve a imagem reduzida (JPEG). Se algo falhar, devolve a original.</summary>
    public static (byte[] Data, string Extension) Shrink(byte[] data, string extension)
    {
        try
        {
            using var input = new MemoryStream(data);
            var image = PlatformImage.FromStream(input);
            if (image is null)
                return (data, extension);

            using var small = image.Downsize(MaxSide, true);
            using var output = new MemoryStream();
            small.Save(output, ImageFormat.Jpeg, 0.85f);
            return (output.ToArray(), ".jpg");
        }
        catch (Exception)
        {
            return (data, extension);
        }
    }
}
