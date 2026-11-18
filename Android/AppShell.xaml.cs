namespace SpinVaultMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AlbumDetailPage), typeof(AlbumDetailPage));
        Routing.RegisterRoute(nameof(AlbumEditPage), typeof(AlbumEditPage));
    }
}
