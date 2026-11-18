using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using SpinVaultMobile.Cloud;

namespace SpinVaultMobile;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        VersionLabel.Text = Loc.F("Versão {0}", AppInfo.VersionString);
        MarkLanguage();
        MarkTheme();
        RefreshCloud();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshCloud();
    }

    // ---------- Nuvem (Google Drive) ----------

    private void RefreshCloud()
    {
        var signedIn = GoogleAuth.IsSignedIn;
        CloudSignInButton.IsVisible = !signedIn;
        CloudSyncButton.IsVisible = signedIn;
        CloudSignOutButton.IsVisible = signedIn;

        if (!signedIn)
        {
            CloudStatusLabel.Text = Loc.T("Entre com o Google para guardar a coleção na nuvem e restaurá-la em outro aparelho.");
            return;
        }

        var text = Loc.F("Conectado como {0}", GoogleAuth.Email);
        var last = CloudSync.LastSyncText;
        if (last.Length > 0)
            text += "\n" + Loc.F("Última sincronização: {0}", last);
        CloudStatusLabel.Text = text;
    }

    private async void OnCloudSignInClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!await GoogleAuth.SignInAsync())
                return;
            RefreshCloud();
            await CloudSync.ReconcileAsync(true);
            RefreshCloud();
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("Não foi possível entrar"), ex.Message, Loc.T("Fechar"));
        }
    }

    private async void OnCloudSyncClicked(object? sender, EventArgs e)
    {
        await CloudSync.ReconcileAsync(true);
        RefreshCloud();
    }

    private async void OnCloudSignOutClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlert(Loc.T("Sair da conta"), Loc.T("Sair da conta Google? O backup na nuvem continua guardado."), Loc.T("Sair da conta"), Loc.T("Cancelar")))
            return;
        GoogleAuth.SignOut();
        RefreshCloud();
    }

    // ---------- Idioma ----------

    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    private static void Mark(Button button, bool selected)
    {
        button.BackgroundColor = selected ? Res("Accent") : Res("SurfaceAlt");
        button.TextColor = selected ? Res("AccentFg") : Res("TextPrimary");
    }

    private void MarkLanguage()
    {
        Mark(PortugueseButton, Loc.Code == Loc.Portuguese);
        Mark(EnglishButton, Loc.Code == Loc.English);
    }

    // ---------- Tema ----------

    private void MarkTheme()
    {
        Mark(DarkButton, ThemeManager.IsDark);
        Mark(LightButton, !ThemeManager.IsDark);
    }

    private void OnDarkClicked(object? sender, EventArgs e) => ChangeTheme(true);

    private void OnLightClicked(object? sender, EventArgs e) => ChangeTheme(false);

    private void ChangeTheme(bool dark)
    {
        if (ThemeManager.IsDark == dark)
            return;

        ThemeManager.Set(dark);
        ThemeManager.Apply();
        App.Reload();   // recria as telas já com as cores do novo tema
    }

    private void OnPortugueseClicked(object? sender, EventArgs e) => ChangeLanguage(Loc.Portuguese);

    private void OnEnglishClicked(object? sender, EventArgs e) => ChangeLanguage(Loc.English);

    private void ChangeLanguage(string code)
    {
        if (Loc.Code == code)
            return;

        SettingsStore.Current.Language = code;
        SettingsStore.Save();
        Loc.Set(code);
        App.Reload();   // recria as telas já no novo idioma
    }

    // ---------- Capas e dados em lote ----------

    private async void OnDownloadCoversClicked(object? sender, EventArgs e)
    {
        var missing = AppData.Repo.GetAll(false).Concat(AppData.Repo.GetAll(true))
            .Where(a => CoverService.Resolve(a.CoverFile) is null).ToList();
        if (missing.Count == 0)
        {
            await DisplayAlert(Loc.T("Baixar capas"), Loc.T("Todos os álbuns já têm capa."), Loc.T("Fechar"));
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 3 / 60.0));
        var question = Loc.F("Buscar na internet a capa de {0} álbuns que ainda não têm?\n\n", missing.Count) +
                       Loc.F("Leva em torno de {0} min (o serviço gratuito permite cerca de 1 consulta por segundo). ", minutes) +
                       Loc.T("Você pode cancelar quando quiser e o que já foi baixado fica salvo.");
        if (!await DisplayAlert(Loc.T("Baixar capas"), question, Loc.T("Baixar capas"), Loc.T("Cancelar")))
            return;

        await Navigation.PushModalAsync(new BatchPage(true, missing));
    }

    private async void OnCompleteDatesClicked(object? sender, EventArgs e)
    {
        var missing = AppData.Repo.GetAll(false).Concat(AppData.Repo.GetAll(true))
            .Where(a => a.ReleaseDate is null).ToList();
        if (missing.Count == 0)
        {
            await DisplayAlert(Loc.T("Completar dados"), Loc.T("Todos os álbuns já têm data de lançamento."), Loc.T("Fechar"));
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 2 / 60.0));
        var question = Loc.F("Buscar na internet a data de lançamento de {0} álbuns que estão sem ela?\n\n", missing.Count) +
                       Loc.F("Leva em torno de {0} min. Só preenche quando o resultado é confiável (mesmo artista); ", minutes) +
                       Loc.T("o que não achar fica em branco. Você pode cancelar quando quiser.");
        if (!await DisplayAlert(Loc.T("Completar dados"), question, Loc.T("Completar dados"), Loc.T("Cancelar")))
            return;

        await Navigation.PushModalAsync(new BatchPage(false, missing));
    }

    // ---------- Importar ----------

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = new[]
                {
                    "text/csv", "text/comma-separated-values", "application/csv", "text/plain",
                    "application/vnd.ms-excel", "application/octet-stream",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                },
            });
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Loc.T("Escolha a planilha ou o CSV da coleção"),
                FileTypes = types,
            });
            if (file is null)
                return;

            using var input = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            memory.Position = 0;

            var warnings = new List<string>();
            IReadOnlyList<Album> albums = Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? new CsvImporter().Read(memory, warnings)
                : new ExcelImporter().Read(memory, warnings);

            var repo = AppData.Repo;
            var toAdd = new List<Album>();
            var skipped = 0;
            foreach (var album in albums)
            {
                if (repo.Exists(album.Title, album.Artist, album.Format, null, album.IsWishlist))
                    skipped++;
                else
                    toAdd.Add(album);
            }

            repo.AddRange(toAdd);

            var message = Loc.F("{0} itens importados.", toAdd.Count);
            if (skipped > 0)
                message += "\n" + Loc.F("{0} já existiam e foram ignorados.", skipped);
            await DisplayAlert(Loc.T("Importação concluída"), message, Loc.T("Fechar"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or InvalidOperationException)
        {
            await DisplayAlert(Loc.T("Não foi possível importar"), ex.Message, Loc.T("Fechar"));
        }
    }

    // ---------- Exportar ----------

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var albums = AppData.Repo.GetAll(false);
            if (albums.Count == 0)
            {
                await DisplayAlert(Loc.T("Exportar coleção"), Loc.T("Não há álbuns para exportar."), Loc.T("Fechar"));
                return;
            }

            var excel = Loc.T("Planilha do Excel");
            var csv = Loc.T("Texto CSV");
            var choice = await DisplayActionSheet(Loc.T("Exportar coleção"), Loc.T("Cancelar"), null, excel, csv);
            if (choice != excel && choice != csv)
                return;

            var extension = choice == csv ? ".csv" : ".xlsx";
            var path = Path.Combine(FileSystem.CacheDirectory, $"spinvault-{DateTime.Now:yyyy-MM-dd}{extension}");
            ExportService.Export(path, albums);
            await Share.Default.RequestAsync(new ShareFileRequest(Loc.T("Exportar coleção"), new ShareFile(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await DisplayAlert(Loc.T("Não foi possível exportar"), ex.Message, Loc.T("Fechar"));
        }
    }

    // ---------- Backup ----------

    private async void OnBackupClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"backup-spinvault-{DateTime.Now:yyyy-MM-dd-HHmm}.zip");
            BackupService.CreateBackup(Database.AppFolder, path);
            await Share.Default.RequestAsync(new ShareFileRequest(Loc.T("Backup da coleção"), new ShareFile(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await DisplayAlert(Loc.T("Não foi possível criar o backup"), ex.Message, Loc.T("Fechar"));
        }
    }

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        string? temp = null;
        try
        {
            var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = new[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" },
            });
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Loc.T("Escolha o backup para restaurar"),
                FileTypes = types,
            });
            if (file is null)
                return;

            temp = Path.Combine(FileSystem.CacheDirectory, $"restaurar-{Guid.NewGuid():N}.zip");
            await using (var input = await file.OpenReadAsync())
            await using (var output = File.Create(temp))
                await input.CopyToAsync(output);

            var (albums, covers) = BackupService.Inspect(temp);
            var message = Loc.F("Esse backup tem {0} álbuns e {1} capas.", albums, covers) + "\n\n" +
                          Loc.T("Restaurar vai SUBSTITUIR a coleção atual por esse backup.");
            if (!await DisplayAlert(Loc.T("Restaurar backup"), message, Loc.T("Restaurar backup"), Loc.T("Cancelar")))
                return;

            BackupService.Restore(Database.AppFolder, temp, out _);
            await DisplayAlert(Loc.T("Backup restaurado"), Loc.F("{0} álbuns", albums), Loc.T("Fechar"));
            App.Reload();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            await DisplayAlert(Loc.T("Não foi possível restaurar"), ex.Message, Loc.T("Fechar"));
        }
        finally
        {
            if (temp is not null)
                try { File.Delete(temp); } catch (IOException) { }
        }
    }
}
