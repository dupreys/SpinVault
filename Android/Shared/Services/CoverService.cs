using ColecaoMidias.Data;

namespace ColecaoMidias.Services;

/// <summary>Guarda as capas numa pasta do app. No banco fica só o nome do arquivo.</summary>
public static class CoverService
{
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

    public static bool IsSupportedImage(string path) =>
        AllowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Copia a imagem escolhida para a pasta de capas e devolve o nome do novo arquivo.</summary>
    public static string Import(string sourcePath)
    {
        Directory.CreateDirectory(Database.CoversFolder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        File.Copy(sourcePath, Path.Combine(Database.CoversFolder, fileName), overwrite: false);
        return fileName;
    }

    /// <summary>Grava uma imagem baixada da internet na pasta de capas e devolve o nome do arquivo.</summary>
    public static string SaveBytes(byte[] data, string extension)
    {
        Directory.CreateDirectory(Database.CoversFolder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        File.WriteAllBytes(Path.Combine(Database.CoversFolder, fileName), data);
        return fileName;
    }

    public static string? Resolve(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = Path.Combine(Database.CoversFolder, fileName);
        return File.Exists(path) ? path : null;
    }

    public static void Delete(string? fileName)
    {
        var path = Resolve(fileName);
        if (path is null)
            return;

        try { File.Delete(path); }
        catch (IOException) { /* arquivo em uso: não é crítico */ }
    }
}
