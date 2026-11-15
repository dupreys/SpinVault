using System.Text.Json;

namespace ColecaoMidias.Services;

/// <summary>Configurações do usuário (backup automático e atualizações), guardadas em settings.json.</summary>
public sealed class AppSettings
{
    // ----- Backup automático -----
    public bool AutoBackupEnabled { get; set; } = true;

    /// <summary>Pasta escolhida pelo usuário (ex.: dentro do OneDrive). Vazio = pasta padrão do app.</summary>
    public string? BackupFolder { get; set; }

    /// <summary>De quantos em quantos dias fazer um backup (se algo mudou).</summary>
    public int BackupIntervalDays { get; set; } = 7;

    /// <summary>Quantos backups automáticos manter; os mais antigos são apagados.</summary>
    public int BackupKeep { get; set; } = 10;

    public DateTime? LastBackupUtc { get; set; }
    public string? LastBackupFile { get; set; }
    public string? LastBackupError { get; set; }

    // ----- Atualizações -----
    public bool UpdateCheckEnabled { get; set; } = true;
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>Versão que o usuário pediu para não avisar mais.</summary>
    public string? SkippedVersion { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Lê o arquivo; se não existir ou estiver corrompido, devolve os valores padrão (nunca falha).</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // arquivo ilegível: recomeça com o padrão
        }
        return new AppSettings();
    }

    /// <summary>Grava de forma segura (arquivo temporário e troca), para não corromper se o PC desligar no meio.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, path, overwrite: true);
    }

    private static AppSettings Normalize(AppSettings s)
    {
        if (s.BackupIntervalDays < 1) s.BackupIntervalDays = 7;
        if (s.BackupKeep < 1) s.BackupKeep = 10;
        return s;
    }
}

/// <summary>Instância única das configurações do app.</summary>
public static class SettingsStore
{
    private static readonly object Gate = new();
    private static AppSettings? _current;

    public static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColecaoMidias", "settings.json");

    public static AppSettings Current
    {
        get
        {
            lock (Gate)
                return _current ??= AppSettings.Load(FilePath);
        }
    }

    public static void Save()
    {
        lock (Gate)
        {
            try { Current.Save(FilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* configuração é best-effort */ }
        }
    }
}
