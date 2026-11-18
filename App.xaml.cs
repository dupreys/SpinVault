using SpinVaultMobile.Cloud;

namespace SpinVaultMobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeManager.Apply();
        MainPage = new AppShell();
    }

    /// <summary>Recria todas as telas (usado ao trocar o idioma ou restaurar um backup).</summary>
    public static void Reload()
    {
        if (Current is App app)
            app.MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        base.OnStart();
        // Espera a tela abrir e confere a nuvem (envia ou oferece restaurar).
        _ = Task.Run(async () =>
        {
            await Task.Delay(2500);
            await MainThread.InvokeOnMainThreadAsync(() => CloudSync.ReconcileAsync(false));
        });
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _ = CloudSync.BackupIfChangedAsync();
    }
}
