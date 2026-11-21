using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>Backup encontrado na pasta de sincronização.</summary>
public sealed record SyncRemote(string Stamp, string ZipPath, DateTime ModifiedLocal);

/// <summary>Resultado de comparar este computador com a pasta de sincronização.</summary>
public sealed record SyncCheck(SyncAction Action, SyncRemote? Remote);

/// <summary>
/// Sincronização entre aparelhos por uma pasta comum (a pasta do OneDrive, Google Drive ou Dropbox do próprio usuário).
/// Não usa conta nem servidor: o serviço de nuvem do usuário leva o arquivo de um aparelho ao outro.
/// Esta classe só mexe em arquivos; as perguntas ao usuário ficam na tela.
/// </summary>
public sealed class FolderSyncService
{
    private readonly string _appFolder;
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly Func<List<Album>> _albums;

    public FolderSyncService(string appFolder, AppSettings settings, Action saveSettings, Func<List<Album>> albums)
    {
        _appFolder = appFolder;
        _settings = settings;
        _saveSettings = saveSettings;
        _albums = albums;
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_settings.SyncFolder);
    public string Folder => _settings.SyncFolder ?? "";

    public string LastSyncText
    {
        get
        {
            if (_settings.LastSyncUtc is not { } last)
                return "";
            var local = last.ToLocalTime();
            return Loc.IsEnglish
                ? local.ToString("MM/dd/yyyy 'at' h:mm tt", Fmt.Current)
                : local.ToString("dd/MM/yyyy 'às' HH:mm", Fmt.Current);
        }
    }

    /// <summary>Liga a sincronização numa pasta. Recomeça do zero: o primeiro encontro com a pasta sempre confere os dois lados.</summary>
    public void SetFolder(string? folder)
    {
        _settings.SyncFolder = string.IsNullOrWhiteSpace(folder) ? null : folder;
        _settings.SyncLocalFingerprint = null;
        _settings.SyncRemoteStamp = null;
        _settings.LastSyncUtc = null;
        _saveSettings();
    }

    /// <summary>Procura o backup na pasta. Null se ainda não existe; lança exceção se a pasta não está acessível.</summary>
    public SyncRemote? FindRemote()
    {
        if (!Directory.Exists(Folder))
            throw new DirectoryNotFoundException(Loc.F("A pasta de sincronização não foi encontrada: {0}", Folder));

        var zip = Path.Combine(Folder, SyncPlanner.ZipName);
        if (!File.Exists(zip))
            return null;

        var info = new FileInfo(zip);
        string? manifestText = null;
        try
        {
            var manifestPath = Path.Combine(Folder, SyncPlanner.ManifestName);
            if (File.Exists(manifestPath))
                manifestText = File.ReadAllText(manifestPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        var manifest = SyncManifest.TryParse(manifestText);
        var stamp = manifest?.Fingerprint ?? $"zip:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        return new SyncRemote(stamp, zip, info.LastWriteTime);
    }

    public SyncCheck Check()
    {
        var albums = _albums();
        var localEmpty = albums.Count == 0;
        var localChanged = !localEmpty && SyncPlanner.Fingerprint(albums) != (_settings.SyncLocalFingerprint ?? "");

        var remote = FindRemote();
        var remoteChanged = remote is not null && remote.Stamp != (_settings.SyncRemoteStamp ?? "");
        return new SyncCheck(SyncPlanner.Plan(remote is not null, remoteChanged, localEmpty, localChanged), remote);
    }

    /// <summary>Ao fechar o app: envia só se este computador mudou e a pasta não foi alterada por outro aparelho.</summary>
    public bool ShouldUploadOnExit()
    {
        if (!Enabled || !Directory.Exists(Folder))
            return false;

        var albums = _albums();
        if (albums.Count == 0 || SyncPlanner.Fingerprint(albums) == (_settings.SyncLocalFingerprint ?? ""))
            return false;

        var remote = FindRemote();
        return remote is null || remote.Stamp == (_settings.SyncRemoteStamp ?? "");
    }

    public void Upload()
    {
        var albums = _albums();
        var fingerprint = SyncPlanner.Fingerprint(albums);
        Directory.CreateDirectory(Folder);

        var zip = Path.Combine(Folder, SyncPlanner.ZipName);
        var tempZip = zip + ".tmp";
        try
        {
            BackupService.CreateBackup(_appFolder, tempZip);
            File.Move(tempZip, zip, overwrite: true);
        }
        finally
        {
            try { File.Delete(tempZip); } catch (IOException) { }
        }

        var manifest = new SyncManifest(fingerprint, Environment.MachineName, DateTime.UtcNow);
        var manifestPath = Path.Combine(Folder, SyncPlanner.ManifestName);
        File.WriteAllText(manifestPath + ".tmp", manifest.ToJson());
        File.Move(manifestPath + ".tmp", manifestPath, overwrite: true);

        _settings.SyncLocalFingerprint = fingerprint;
        _settings.SyncRemoteStamp = fingerprint;
        _settings.LastSyncUtc = DateTime.UtcNow;
        _saveSettings();
    }

    /// <summary>Quantos álbuns e capas o backup da pasta tem.</summary>
    public (int Albums, int Covers) Inspect(SyncRemote remote) => WithCopy(remote, BackupService.Inspect);

    /// <summary>Troca a coleção deste computador pela da pasta (o BackupService guarda uma cópia de segurança antes).</summary>
    public void Restore(SyncRemote remote)
    {
        WithCopy(remote, copy =>
        {
            BackupService.Restore(_appFolder, copy, out _);
            return 0;
        });

        _settings.SyncLocalFingerprint = SyncPlanner.Fingerprint(_albums());
        _settings.SyncRemoteStamp = remote.Stamp;
        _settings.LastSyncUtc = DateTime.UtcNow;
        _saveSettings();
    }

    // Trabalha numa cópia, para não travar o arquivo enquanto o OneDrive ou o Drive ainda mexem nele.
    private static T WithCopy<T>(SyncRemote remote, Func<string, T> action)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"spinvault-sync-{Guid.NewGuid():N}.zip");
        try
        {
            File.Copy(remote.ZipPath, copy, overwrite: true);
            return action(copy);
        }
        finally
        {
            try { File.Delete(copy); } catch (IOException) { }
        }
    }
}
