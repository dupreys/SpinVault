using ColecaoMidias.Data;

namespace SpinVaultMobile;

/// <summary>Banco e repositório do app (o mesmo SQLite do Windows, guardado na pasta privada do aplicativo).</summary>
public static class AppData
{
    private static readonly Lazy<Database> Db = new(() => new Database());

    public static AlbumRepository Repo => new(Db.Value);
}
