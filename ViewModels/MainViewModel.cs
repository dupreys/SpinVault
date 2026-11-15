using ColecaoMidias.Data;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public CollectionViewModel Collection { get; }
    public DashboardViewModel Dashboard { get; }
    public WishlistViewModel Wishlist { get; }

    /// <summary>"collection", "wishlist" ou "dashboard" (ligado aos botões do menu lateral).</summary>
    [ObservableProperty]
    private string pageKey = "collection";

    [ObservableProperty]
    private string themeButtonText = "";

    public MainViewModel()
    {
        var repository = new AlbumRepository(new Database());
        Collection = new CollectionViewModel(repository);
        Dashboard = new DashboardViewModel(repository);
        Wishlist = new WishlistViewModel(repository);

        // Um desejo comprado entra na coleção: recarrega a coleção e mostra o álbum novo.
        Wishlist.Purchased += id =>
        {
            Collection.Reload(id);
            PageKey = "collection";
        };

        // O dashboard só recalcula quando algo mudou na coleção (adicionar, editar, excluir, importar).
        Collection.DataChanged += () => _dashboardStale = true;

        UpdateThemeButtonText();
    }

    private bool _dashboardStale;

    partial void OnPageKeyChanged(string value)
    {
        if (value == "dashboard" && _dashboardStale)
        {
            Dashboard.Refresh();
            _dashboardStale = false;
        }
    }

    [RelayCommand]
    private void ShowCollection() => PageKey = "collection";

    [RelayCommand]
    private void ShowWishlist() => PageKey = "wishlist";

    [RelayCommand]
    private void ShowDashboard() => PageKey = "dashboard";

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.Toggle();
        UpdateThemeButtonText();
    }

    private void UpdateThemeButtonText() =>
        ThemeButtonText = ThemeService.Current == AppTheme.Dark ? "☀   Tema claro" : "☾   Tema escuro";
}
