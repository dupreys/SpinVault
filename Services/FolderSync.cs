using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>O que fazer ao comparar este aparelho com o backup da pasta de sincronização.</summary>
public enum SyncAction
{
    /// <summary>Não há backup na pasta e a coleção daqui está vazia.</summary>
    NothingEmpty,
    /// <summary>Nada mudou nos dois lados.</summary>
    UpToDate,
    /// <summary>Só este aparelho mudou: envia para a pasta.</summary>
    Upload,
    /// <summary>Só a pasta mudou (ou este aparelho está vazio): oferece restaurar.</summary>
    Restore,
    /// <summary>Os dois lados mudaram: quem decide é o usuário.</summary>
    Ask,
}

/// <summary>
/// Regras da sincronização por pasta, iguais no Windows e no Android.
/// O app guarda na pasta escolhida (por exemplo, dentro do OneDrive, Google Drive ou Dropbox)
/// o arquivo <see cref="ZipName"/> com a coleção e o <see cref="ManifestName"/> com a "impressão digital" dela.
/// Nunca troca uma coleção por outra sem perguntar, a não ser que este aparelho não tenha alterações novas.
/// </summary>
public static class SyncPlanner
{
    public const string ZipName = "spinvault-sync.zip";
    public const string ManifestName = "spinvault-sync.json";

    public static SyncAction Plan(bool remoteExists, bool remoteChanged, bool localEmpty, bool localChanged)
    {
        if (!remoteExists)
            return localEmpty ? SyncAction.NothingEmpty : SyncAction.Upload;

        if (!remoteChanged && !localChanged)
            return SyncAction.UpToDate;

        if (!remoteChanged)
            return SyncAction.Upload;

        if (localEmpty || !localChanged)
            return SyncAction.Restore;

        return SyncAction.Ask;
    }

    /// <summary>Resumo do conteúdo da coleção (coleção + lista de desejos). Muda quando qualquer álbum muda.</summary>
    public static string Fingerprint(IEnumerable<Album> albums) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", albums.OrderBy(a => a.Id).Select(a => a.Fingerprint)))));
}

/// <summary>Pequeno arquivo ao lado do .zip: diz qual coleção está dentro dele, sem precisar abri-lo.</summary>
public sealed record SyncManifest(string Fingerprint, string Device, DateTime SavedUtc)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static SyncManifest? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var manifest = JsonSerializer.Deserialize<SyncManifest>(json);
            return manifest is { Fingerprint.Length: > 0 } ? manifest : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
