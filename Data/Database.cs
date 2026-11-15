using Microsoft.Data.Sqlite;

namespace ColecaoMidias.Data;

/// <summary>
/// Banco SQLite local. O arquivo fica em %LocalAppData%\ColecaoMidias\colecao.db
/// e as capas em %LocalAppData%\ColecaoMidias\Covers.
/// </summary>
public sealed class Database
{
    public static string AppFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColecaoMidias");

    public static string CoversFolder { get; } = Path.Combine(AppFolder, "Covers");

    private readonly string _connectionString;

    public Database(string? filePath = null)
    {
        Directory.CreateDirectory(AppFolder);
        Directory.CreateDirectory(CoversFolder);

        var path = filePath ?? Path.Combine(AppFolder, "colecao.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        Initialize();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Albums (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Title        TEXT    NOT NULL,
                Artist       TEXT    NOT NULL,
                Format       INTEGER NOT NULL,
                ReleaseDate  TEXT    NULL,
                PurchaseYear INTEGER NULL,
                PriceCents   INTEGER NOT NULL DEFAULT 0,
                CoverFile    TEXT    NULL,
                Notes        TEXT    NULL,
                CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS IX_Albums_Artist ON Albums (Artist COLLATE NOCASE);
            """;
        command.ExecuteNonQuery();

        // Bancos de versões anteriores não têm as colunas novas: acrescenta sem perder nada.
        EnsureColumn(connection, "IsWishlist", "INTEGER NOT NULL DEFAULT 0");   // v1.0 (lista de desejos)
        EnsureColumn(connection, "Genre", "TEXT NULL");                          // v1.1
        EnsureColumn(connection, "Tags", "TEXT NULL");
        EnsureColumn(connection, "Rating", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "MediaCondition", "TEXT NULL");
        EnsureColumn(connection, "CoverCondition", "TEXT NULL");
        EnsureColumn(connection, "PurchasePlace", "TEXT NULL");
    }

    private static void EnsureColumn(SqliteConnection connection, string name, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Albums') WHERE name = $name";
        check.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(check.ExecuteScalar()) != 0)
            return;

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE Albums ADD COLUMN {name} {definition}";
        alter.ExecuteNonQuery();
    }
}
