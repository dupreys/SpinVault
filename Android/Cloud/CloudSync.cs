using System.Security.Cryptography;
using System.Text;
using ColecaoMidias.Data;
using ColecaoMidias.Services;

namespace SpinVaultMobile.Cloud;

/// <summary>
/// Mantém a coleção igual na nuvem (Google Drive) e neste aparelho.
/// Regra: nunca troca uma coleção por outra sem perguntar, a não ser que este aparelho não tenha alterações novas.
/// </summary>
public static class CloudSync
{
    private const string LocalKey = "sync_local_fp";     // estado deste aparelho na última sincronização
    private const string RemoteKey = "sync_remote_mod";   // data do backup da nuvem na última sincronização
    private const string LastKey = "sync_last";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static Page? Ui => Shell.Current;

    public static string LastSyncText
    {
        get
        {
            var ticks = Preferences.Get(LastKey, 0L);
            return ticks == 0 ? "" : new DateTime(ticks).ToString("g", Loc.Culture);
        }
    }

    private static List<ColecaoMidias.Models.Album> LocalAlbums()
    {
        var repo = AppData.Repo;
        return repo.GetAll(false).Concat(repo.GetAll(true)).OrderBy(a => a.Id).ToList();
    }

    private static string Fingerprint(List<ColecaoMidias.Models.Album> albums) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", albums.Select(a => a.Fingerprint)))));

    private static Task Alert(string message) =>
        Ui?.DisplayAlert(Loc.T("Backup na nuvem"), message, Loc.T("Fechar")) ?? Task.CompletedTask;

    /// <summary>Compara este aparelho com a nuvem e resolve (envia, restaura ou pergunta).</summary>
    public static async Task ReconcileAsync(bool manual)
    {
        if (!GoogleAuth.IsSignedIn || !await Gate.WaitAsync(0))
            return;
        try
        {
            var remote = await DriveClient.FindBackupAsync();
            var albums = LocalAlbums();
            var localEmpty = albums.Count == 0;
            var localChanged = !localEmpty && Fingerprint(albums) != Preferences.Get(LocalKey, "");

            if (remote is null)
            {
                if (!localEmpty)
                {
                    await UploadAsync(null, albums);
                    if (manual) await Alert(Loc.T("Coleção enviada para a nuvem."));
                }
                else if (manual)
                {
                    await Alert(Loc.T("Ainda não há backup na nuvem e esta coleção está vazia."));
                }
                return;
            }

            var remoteChanged = remote.Modified != Preferences.Get(RemoteKey, "");

            if (!remoteChanged && !localChanged)
            {
                if (manual) await Alert(Loc.T("Tudo certo: a coleção já está igual à da nuvem."));
                return;
            }

            if (!remoteChanged)
            {
                await UploadAsync(remote.Id, albums);
                if (manual) await Alert(Loc.T("Coleção enviada para a nuvem."));
                return;
            }

            if (localEmpty || !localChanged)
            {
                await RestoreAsync(remote, localEmpty
                    ? Loc.T("Encontrei a sua coleção na nuvem. Restaurar neste aparelho?")
                    : Loc.T("Há um backup mais novo na nuvem. Restaurar neste aparelho? A coleção daqui será substituída."));
                return;
            }

            // Os dois lados mudaram: quem decide é o usuário.
            var useCloud = Loc.T("Usar a coleção da nuvem");
            var useDevice = Loc.T("Enviar a coleção deste aparelho");
            var choice = await Ui!.DisplayActionSheet(
                Loc.T("Esta coleção e a da nuvem são diferentes. O que fazer?"),
                Loc.T("Agora não"), null, useCloud, useDevice);
            if (choice == useCloud)
                await RestoreAsync(remote, null);
            else if (choice == useDevice)
                await UploadAsync(remote.Id, albums);
        }
        catch (AuthExpiredException)
        {
            await Alert(Loc.T("Sessão expirada. Entre com o Google de novo."));
        }
        catch (Exception ex) when (manual)
        {
            await Alert(Loc.T("Não foi possível sincronizar") + "\n\n" + ex.Message);
        }
        catch (Exception)
        {
            // Sem internet ou erro passageiro: tenta de novo na próxima vez que o app abrir.
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Ao sair do app: envia as alterações, mas só se a nuvem não tiver mudado por outro aparelho.</summary>
    public static async Task BackupIfChangedAsync()
    {
        if (!GoogleAuth.IsSignedIn || !await Gate.WaitAsync(0))
            return;
        try
        {
            var albums = LocalAlbums();
            if (albums.Count == 0 || Fingerprint(albums) == Preferences.Get(LocalKey, ""))
                return;

            var remote = await DriveClient.FindBackupAsync();
            if (remote is not null && remote.Modified != Preferences.Get(RemoteKey, ""))
                return;   // outro aparelho mexeu: a decisão fica para a próxima abertura

            await UploadAsync(remote?.Id, albums);
        }
        catch (Exception)
        {
            // Sem internet: a próxima abertura do app resolve.
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task UploadAsync(string? existingId, List<ColecaoMidias.Models.Album> albums)
    {
        var fingerprint = Fingerprint(albums);
        var temp = Path.Combine(FileSystem.CacheDirectory, "nuvem-envio.zip");
        try
        {
            await Task.Run(() => BackupService.CreateBackup(Database.AppFolder, temp));
            var file = await DriveClient.UploadAsync(temp, existingId);
            Preferences.Set(LocalKey, fingerprint);
            Preferences.Set(RemoteKey, file.Modified);
            Preferences.Set(LastKey, DateTime.Now.Ticks);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    // question == null: a pessoa já escolheu usar a nuvem, então não pergunta de novo.
    private static async Task RestoreAsync(DriveFile remote, string? question)
    {
        var temp = Path.Combine(FileSystem.CacheDirectory, "nuvem-restaurar.zip");
        try
        {
            await DriveClient.DownloadAsync(remote.Id, temp);
            var (albums, covers) = await Task.Run(() => BackupService.Inspect(temp));

            if (question is not null)
            {
                var message = question + "\n\n" +
                              Loc.F("Backup na nuvem: {0} álbuns e {1} capas, de {2}.", albums, covers, remote.ModifiedLocal.ToString("g", Loc.Culture));
                if (!await Ui!.DisplayAlert(Loc.T("Backup na nuvem"), message, Loc.T("Restaurar"), Loc.T("Agora não")))
                    return;
            }

            await Task.Run(() => BackupService.Restore(Database.AppFolder, temp, out _));
            Preferences.Set(LocalKey, Fingerprint(LocalAlbums()));
            Preferences.Set(RemoteKey, remote.Modified);
            Preferences.Set(LastKey, DateTime.Now.Ticks);
            App.Reload();
            await Alert(Loc.T("Coleção restaurada da nuvem."));
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }
}
