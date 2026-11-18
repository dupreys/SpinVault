using ColecaoMidias.Services;

namespace SpinVaultMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // O idioma precisa vir antes de qualquer tela (segue o aparelho até o usuário escolher).
        Loc.Init(SettingsStore.Current.Language);

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}
