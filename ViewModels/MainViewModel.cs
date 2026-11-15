using System.Windows.Threading;
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
    public InsightsViewModel Insights { get; }

    /// <summary>"collection", "wishlist", "dashboard" ou "insights" (ligado aos botões do menu lateral).</summary>
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
        Insights = new InsightsViewModel(repository);

        // Um desejo comprado entra na coleção: recarrega a coleção e mostra o álbum novo.
        Wishlist.Purchased += id =>
        {
            Collection.Reload(id);
            PageKey = "collection";
        };

        // O dashboard só recalcula quando algo mudou na coleção (adicionar, editar, excluir, importar).
        Collection.DataChanged += () => _dashboardStale = true;

        _autoBackup = new AutoBackupService(Database.AppFolder, SettingsStore.Current, SettingsStore.Save);

        UpdateThemeButtonText();
    }

    private bool _dashboardStale;

    // ---------- Backup automático e aviso de atualização ----------

    private readonly AutoBackupService _autoBackup;
    private readonly UpdateService _updateService = UpdateService.Create();
    private DispatcherTimer? _timer;
    private UpdateInfo? _pendingUpdate;

    [ObservableProperty]
    private bool updateBannerVisible;

    [ObservableProperty]
    private string updateBannerText = "";

    /// <summary>Chamado quando a janela principal abre: agenda o backup automático e a verificação de versão.</summary>
    public void StartBackgroundTasks()
    {
        // Espera um pouco para não competir com a abertura do app.
        _ = RunStartupTasksAsync();

        // Se o app ficar aberto por dias, volta a olhar de tempos em tempos.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _timer.Tick += async (_, _) => await RunBackupIfDueAsync();
        _timer.Start();
    }

    private async Task RunStartupTasksAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(6));
        await RunBackupIfDueAsync();

        var settings = SettingsStore.Current;
        if (settings.UpdateCheckEnabled
            && (settings.LastUpdateCheckUtc is not { } last || DateTime.UtcNow - last > TimeSpan.FromHours(24)))
        {
            await CheckForUpdatesAsync(manual: false);
        }
    }

    private async Task RunBackupIfDueAsync()
    {
        try
        {
            await Task.Run(() => _autoBackup.RunIfDue());
        }
        catch (Exception)
        {
            // O backup automático nunca deve atrapalhar o uso do app; o erro fica nas Configurações.
        }
    }

    /// <summary>Consulta o GitHub. Na verificação automática, respeita a versão que o usuário pediu para ignorar.</summary>
    private async Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual)
    {
        var result = await _updateService.CheckAsync();
        var settings = SettingsStore.Current;

        if (!result.Failed)
        {
            settings.LastUpdateCheckUtc = DateTime.UtcNow;
            SettingsStore.Save();
        }

        _pendingUpdate = result.Available;
        var skipped = result.Available is { } found
                      && !manual
                      && string.Equals(settings.SkippedVersion, found.Version, StringComparison.OrdinalIgnoreCase);

        if (result.Available is { } update && !skipped)
        {
            UpdateBannerText = $"Nova versão do SpinVault disponível: {update.Version}  (você tem {UpdateService.Format(_updateService.Current)})";
            UpdateBannerVisible = true;
        }
        else if (result.Available is null)
        {
            UpdateBannerVisible = false;
        }

        return result;
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        if (_pendingUpdate is not null)
            Dialogs.OpenUrl(_pendingUpdate.DownloadUrl ?? _pendingUpdate.PageUrl);
    }

    /// <summary>"Agora não": esconde o aviso e não volta a avisar desta versão (uma versão ainda mais nova avisa de novo).</summary>
    [RelayCommand]
    private void SkipUpdate()
    {
        if (_pendingUpdate is not null)
        {
            SettingsStore.Current.SkippedVersion = _pendingUpdate.Version;
            SettingsStore.Save();
        }
        UpdateBannerVisible = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var version = $"SpinVault {UpdateService.Format(_updateService.Current)}";
        var viewModel = new SettingsViewModel(SettingsStore.Current, _autoBackup, () => CheckForUpdatesAsync(manual: true), version);
        Dialogs.ShowSettings(viewModel);
    }

    partial void OnPageKeyChanged(string value)
    {
        if (value == "dashboard" && _dashboardStale)
        {
            Dashboard.Refresh();
            _dashboardStale = false;
        }

        // O resumo é barato de calcular e depende da data de hoje ("este mês"): recalcula a cada visita.
        if (value == "insights")
            Insights.Refresh();
    }

    [RelayCommand]
    private void ShowCollection() => PageKey = "collection";

    [RelayCommand]
    private void ShowWishlist() => PageKey = "wishlist";

    [RelayCommand]
    private void ShowDashboard() => PageKey = "dashboard";

    [RelayCommand]
    private void ShowInsights() => PageKey = "insights";

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.Toggle();
        UpdateThemeButtonText();
    }

    private void UpdateThemeButtonText() =>
        ThemeButtonText = ThemeService.Current == AppTheme.Dark ? "☀   Tema claro" : "☾   Tema escuro";
}
