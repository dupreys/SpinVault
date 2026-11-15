using System.Globalization;
using ColecaoMidias.Models;
using Microsoft.Data.Sqlite;

namespace ColecaoMidias.Data;

public sealed class AlbumRepository
{
    private const string Columns =
        "Id, Title, Artist, Format, ReleaseDate, PurchaseYear, PriceCents, CoverFile, Notes, IsWishlist";

    private readonly Database _db;

    public AlbumRepository(Database db) => _db = db;

    /// <summary>Álbuns da coleção (padrão) ou, com wishlist=true, os da lista de desejos.</summary>
    public IReadOnlyList<Album> GetAll(bool wishlist = false)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Albums WHERE IsWishlist = $wishlist ORDER BY Artist COLLATE NOCASE, ReleaseDate, Title COLLATE NOCASE";
        command.Parameters.AddWithValue("$wishlist", wishlist ? 1 : 0);

        var list = new List<Album>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    public int Add(Album album)
    {
        using var connection = _db.Open();
        return Insert(connection, null, album);
    }

    /// <summary>Insere vários álbuns numa única transação. Retorna quantos foram inseridos.</summary>
    public int AddRange(IEnumerable<Album> albums)
    {
        using var connection = _db.Open();
        using var transaction = connection.BeginTransaction();

        var count = 0;
        foreach (var album in albums)
        {
            Insert(connection, transaction, album);
            count++;
        }

        transaction.Commit();
        return count;
    }

    public void Update(Album album)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Albums SET
                Title = $title, Artist = $artist, Format = $format, ReleaseDate = $release,
                PurchaseYear = $year, PriceCents = $price, CoverFile = $cover, Notes = $notes, IsWishlist = $wishlist
            WHERE Id = $id
            """;
        Bind(command, album);
        command.Parameters.AddWithValue("$id", album.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Albums WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Já existe um álbum com o mesmo título, artista e mídia? (ignora maiúsculas/minúsculas)</summary>
    public bool Exists(string title, string artist, MediaFormat format, int? excludeId = null, bool wishlist = false)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1) FROM Albums
            WHERE Title = $title COLLATE NOCASE AND Artist = $artist COLLATE NOCASE
              AND Format = $format AND Id <> $exclude AND IsWishlist = $wishlist
            """;
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$artist", artist);
        command.Parameters.AddWithValue("$format", (int)format);
        command.Parameters.AddWithValue("$exclude", excludeId ?? -1);
        command.Parameters.AddWithValue("$wishlist", wishlist ? 1 : 0);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static int Insert(SqliteConnection connection, SqliteTransaction? transaction, Album album)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Albums (Title, Artist, Format, ReleaseDate, PurchaseYear, PriceCents, CoverFile, Notes, IsWishlist)
            VALUES ($title, $artist, $format, $release, $year, $price, $cover, $notes, $wishlist);
            SELECT last_insert_rowid();
            """;
        Bind(command, album);
        album.Id = Convert.ToInt32(command.ExecuteScalar());
        return album.Id;
    }

    private static void Bind(SqliteCommand command, Album album)
    {
        command.Parameters.AddWithValue("$title", album.Title);
        command.Parameters.AddWithValue("$artist", album.Artist);
        command.Parameters.AddWithValue("$format", (int)album.Format);
        command.Parameters.AddWithValue("$release",
            album.ReleaseDate.HasValue
                ? album.ReleaseDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : DBNull.Value);
        command.Parameters.AddWithValue("$year", (object?)album.PurchaseYear ?? DBNull.Value);
        command.Parameters.AddWithValue("$price", (long)Math.Round(album.Price * 100m, MidpointRounding.AwayFromZero));
        command.Parameters.AddWithValue("$cover", (object?)album.CoverFile ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)album.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$wishlist", album.IsWishlist ? 1 : 0);
    }

    private static Album Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Title = r.GetString(1),
        Artist = r.GetString(2),
        Format = (MediaFormat)r.GetInt32(3),
        ReleaseDate = r.IsDBNull(4)
            ? null
            : DateTime.ParseExact(r.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        PurchaseYear = r.IsDBNull(5) ? null : r.GetInt32(5),
        Price = r.GetInt64(6) / 100m,
        CoverFile = r.IsDBNull(7) ? null : r.GetString(7),
        Notes = r.IsDBNull(8) ? null : r.GetString(8),
        IsWishlist = r.GetInt32(9) != 0
    };
}
