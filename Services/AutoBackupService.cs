using System.Globalization;

namespace ColecaoMidias.Services;

public enum BackupResult { Disabled, NotDue, NoChanges, Done, Failed, Busy }

public sealed record BackupOutcome(BackupResult Result, string? File = null, string? Error = null, int Pruned = 0);

/// <summary>
/// Backup automático: de tempos em tempos (e só se algo mudou) grava um .zip na pasta escolhida
/// e apaga os mais antigos, mantendo só os últimos N. Só mexe em arquivos "backup-auto-*.zip".
/// </summary>
public sealed class AutoBackupService
{
    public const string FilePrefix = "backup-auto-";

    private readonly string _appFolder;
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly Action<string, string> _createBackup;
    private readonly Func<DateTime> _utcNow;
    private int _running;

    public AutoBackupService(string appFolder, AppSettings settings, Action saveSettings,
                             Action<string, string>? createBackup = null, Func<DateTime>? utcNow = null)
    {
        _appFolder = appFolder;
        _settings = settings;
        _saveSettings = saveSettings;
        _createBackup = createBackup ?? BackupService.CreateBackup;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public string DefaultFolder => Path.Combine(_appFolder, "Backups", "Automaticos");

    /// <summary>Pasta onde os backups vão (a escolhida ou a padrão).</summary>
    public string Folder => string.IsNullOrWhiteSpace(_settings.BackupFolder) ? DefaultFolder : _settings.BackupFolder!;

    // ---------- Quando fazer ----------

    /// <summary>Decide se já é hora de um backup. <paramref name="dataChangedUtc"/> = última alteração dos dados (null = desconhecida).</summary>
    public static BackupResult Evaluate(AppSettings s, DateTime nowUtc, DateTime? dataChangedUtc)
    {
        if (!s.AutoBackupEnabled)
            return BackupResult.Disabled;

        if (s.LastBackupUtc is not { } last)
            return BackupResult.Done;   // nunca fez: faz agora (sinaliza "pode fazer")

        if (nowUtc < last + TimeSpan.FromDays(Math.Max(1, s.BackupIntervalDays)))
            return BackupResult.NotDue;

        if (dataChangedUtc is { } changed && changed <= last)
            return BackupResult.NoChanges;

        return BackupResult.Done;
    }

    /// <summary>Última vez que o banco ou as capas mudaram.</summary>
    public DateTime? DataLastChangedUtc()
    {
        DateTime? latest = null;
        void Consider(string path)
        {
            try
            {
                var time = File.Exists(path) ? File.GetLastWriteTimeUtc(path)
                         : Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path)
                         : (DateTime?)null;
                if (time is { } t && (latest is null || t > latest))
                    latest = t;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Consider(Path.Combine(_appFolder, "colecao.db"));
        Consider(Path.Combine(_appFolder, "colecao.db-wal"));
        Consider(Path.Combine(_appFolder, "Covers"));
        return latest;
    }

    public BackupOutcome RunIfDue()
    {
        var decision = Evaluate(_settings, _utcNow(), DataLastChangedUtc());
        return decision == BackupResult.Done ? RunNow() : new BackupOutcome(decision);
    }

    // ---------- Fazer ----------

    /// <summary>Faz o backup agora, sem olhar frequência nem mudanças (botão "Fazer backup agora").</summary>
    public BackupOutcome RunNow()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return new BackupOutcome(BackupResult.Busy);

        var started = _utcNow();
        string? temp = null;
        try
        {
            var folder = Folder;
            Directory.CreateDirectory(folder);

            var name = $"{FilePrefix}{DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}.zip";
            var final = Path.Combine(folder, name);
            temp = final + ".tmp";

            _createBackup(_appFolder, temp);
            File.Move(temp, final, overwrite: true);
            temp = null;

            var pruned = Prune(folder, _settings.BackupKeep);

            _settings.LastBackupUtc = started;   // começo do backup: o que mudar durante ele entra no próximo
            _settings.LastBackupFile = final;
            _settings.LastBackupError = null;
            _saveSettings();
            return new BackupOutcome(BackupResult.Done, final, null, pruned);
        }
        catch (Exception ex)
        {
            if (temp is not null)
                try { File.Delete(temp); } catch (Exception) { }

            _settings.LastBackupError = ex.Message;
            _saveSettings();
            return new BackupOutcome(BackupResult.Failed, null, ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <summary>Apaga os backups automáticos mais antigos, deixando só os <paramref name="keep"/> mais recentes. Devolve quantos apagou.</summary>
    public static int Prune(string folder, int keep)
    {
        if (keep < 1 || !Directory.Exists(folder))
            return 0;

        // O nome tem data e hora, então a ordem alfabética é a ordem cronológica.
        var files = Directory.GetFiles(folder, FilePrefix + "*.zip")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)
            .ToList();

        var removed = 0;
        foreach (var old in files.Skip(keep))
        {
            try { File.Delete(old); removed++; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return removed;
    }

    /// <summary>Quantos backups automáticos existem na pasta.</summary>
    public int CountBackups() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, FilePrefix + "*.zip").Length : 0;
}
