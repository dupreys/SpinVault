using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace ColecaoMidias.Services;

/// <summary>
/// Backup completo (banco + capas) num único .zip e restauração a partir dele.
/// Dentro do zip: colecao.db e a pasta Covers/.
/// </summary>
public static class BackupService
{
    private const string DbEntry = "colecao.db";
    private const string CoversPrefix = "Covers/";

    /// <param name="appFolder">Pasta de dados do app (contém colecao.db e Covers).</param>
    public static void CreateBackup(string appFolder, string zipPath)
    {
        var dbPath = Path.Combine(appFolder, "colecao.db");
        var coversFolder = Path.Combine(appFolder, "Covers");
        var tempDb = Path.Combine(Path.GetTempPath(), $"colecao_{Guid.NewGuid():N}.db");

        try
        {
            // VACUUM INTO gera uma cópia consistente do banco mesmo com o app aberto.
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "VACUUM INTO $path";
                command.Parameters.AddWithValue("$path", tempDb);
                command.ExecuteNonQuery();
            }

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var file = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(file, ZipArchiveMode.Create);

            zip.CreateEntryFromFile(tempDb, DbEntry, CompressionLevel.Optimal);

            if (Directory.Exists(coversFolder))
                foreach (var cover in Directory.EnumerateFiles(coversFolder))
                    zip.CreateEntryFromFile(cover, CoversPrefix + Path.GetFileName(cover), CompressionLevel.NoCompression);   // JPG/PNG já são comprimidos
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(tempDb);
        }
    }

    /// <summary>Diz quantos álbuns e capas o backup tem. Lança exceção se o arquivo não for um backup válido.</summary>
    public static (int Albums, int Covers) Inspect(string zipPath)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"colecao_{Guid.NewGuid():N}");
        try
        {
            ExtractTo(zipPath, temp);
            return (CountAlbums(Path.Combine(temp, DbEntry)),
                    Directory.Exists(Path.Combine(temp, "Covers")) ? Directory.GetFiles(Path.Combine(temp, "Covers")).Length : 0);
        }
        finally
        {
            TryDeleteFolder(temp);
        }
    }

    /// <summary>
    /// Substitui o banco e as capas atuais pelo conteúdo do backup. Antes disso guarda uma cópia
    /// de segurança do que existe hoje (devolvida em <paramref name="safetyCopy"/>).
    /// </summary>
    public static void Restore(string appFolder, string zipPath, out string safetyCopy)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"colecao_{Guid.NewGuid():N}");
        try
        {
            ExtractTo(zipPath, temp);
            CountAlbums(Path.Combine(temp, DbEntry));      // valida antes de mexer em qualquer coisa

            var backups = Path.Combine(appFolder, "Backups");
            Directory.CreateDirectory(backups);
            safetyCopy = Path.Combine(backups, $"antes-de-restaurar-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            if (File.Exists(Path.Combine(appFolder, "colecao.db")))
                CreateBackup(appFolder, safetyCopy);

            SqliteConnection.ClearAllPools();               // solta o arquivo do banco

            File.Copy(Path.Combine(temp, DbEntry), Path.Combine(appFolder, "colecao.db"), overwrite: true);
            _ = new Data.Database(Path.Combine(appFolder, "colecao.db"));   // atualiza backups antigos (ex.: sem a coluna da lista de desejos)
            foreach (var extra in new[] { "colecao.db-wal", "colecao.db-shm", "colecao.db-journal" })
                TryDelete(Path.Combine(appFolder, extra));

            var coversFolder = Path.Combine(appFolder, "Covers");
            Directory.CreateDirectory(coversFolder);
            foreach (var old in Directory.GetFiles(coversFolder))
                TryDelete(old);

            var restoredCovers = Path.Combine(temp, "Covers");
            if (Directory.Exists(restoredCovers))
                foreach (var cover in Directory.GetFiles(restoredCovers))
                    File.Copy(cover, Path.Combine(coversFolder, Path.GetFileName(cover)), overwrite: true);
        }
        finally
        {
            TryDeleteFolder(temp);
        }
    }

    // ---------- Internos ----------

    /// <summary>Extrai só o que interessa e só para dentro da pasta indicada (ignora caminhos suspeitos).</summary>
    private static void ExtractTo(string zipPath, string folder)
    {
        Directory.CreateDirectory(folder);
        using var zip = ZipFile.OpenRead(zipPath);

        var db = zip.GetEntry(DbEntry) ?? throw new InvalidDataException(Loc.T("Esse arquivo não é um backup da coleção (falta o banco de dados)."));
        db.ExtractToFile(Path.Combine(folder, DbEntry), overwrite: true);

        var covers = Path.Combine(folder, "Covers");
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(CoversPrefix, StringComparison.OrdinalIgnoreCase) || entry.Name.Length == 0)
                continue;

            Directory.CreateDirectory(covers);
            entry.ExtractToFile(Path.Combine(covers, Path.GetFileName(entry.Name)), overwrite: true);
        }
    }

    private static int CountAlbums(string dbPath)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Albums";
            return Convert.ToInt32(command.ExecuteScalar());
        }
        catch (SqliteException)
        {
            throw new InvalidDataException(Loc.T("O banco de dados dentro do backup está corrompido ou não é da coleção."));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteFolder(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
