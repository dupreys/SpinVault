using System.Windows;
using ColecaoMidias.ViewModels;
using ColecaoMidias.Views;
using Microsoft.Win32;

namespace ColecaoMidias.Services;

/// <summary>Caixas de diálogo usadas pelos ViewModels (mantém o WPF fora da lógica).</summary>
public static class Dialogs
{
    private static Window? Owner => Application.Current.MainWindow;

    public static bool ShowAlbumEditor(AlbumEditViewModel viewModel)
    {
        var window = new AlbumEditWindow(viewModel) { Owner = Owner };
        return window.ShowDialog() == true;
    }

    /// <summary>Abre a janela de download em lote; devolve true se alguma capa foi baixada.</summary>
    public static bool ShowCoverDownload(CoverDownloadViewModel viewModel)
    {
        new CoverDownloadWindow(viewModel) { Owner = Owner }.ShowDialog();
        return viewModel.Changed;
    }

    /// <summary>Abre a janela de completar dados em lote; devolve true se algum álbum foi atualizado.</summary>
    public static bool ShowMetadataBatch(MetadataBatchViewModel viewModel)
    {
        new MetadataBatchWindow(viewModel) { Owner = Owner }.ShowDialog();
        return viewModel.Changed;
    }

    /// <summary>Mostra os álbuns repetidos; devolve true se algum foi excluído.</summary>
    public static bool ShowDuplicates(DuplicatesViewModel viewModel)
    {
        new DuplicatesWindow(viewModel) { Owner = Owner }.ShowDialog();
        return viewModel.Changed;
    }

    public static string? PickExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar coleção",
            Filter = "Planilha do Excel (*.xlsx)|*.xlsx|Texto CSV (*.csv)|*.csv",
            FileName = $"minha-colecao-{DateTime.Now:yyyy-MM-dd}"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public static string? PickBackupSavePath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Salvar backup da coleção",
            Filter = "Backup da coleção (*.zip)|*.zip",
            FileName = $"backup-colecao-{DateTime.Now:yyyy-MM-dd}"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    /// <summary>Escolhe uma pasta (por exemplo, dentro do OneDrive) para os backups automáticos.</summary>
    public static string? PickFolder(string title, string? initial)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dialog.InitialDirectory = initial;
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    /// <summary>Abre uma pasta no Explorador de Arquivos.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception) { }
    }

    /// <summary>Abre um endereço da internet no navegador padrão.</summary>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception) { }
    }

    public static void ShowSettings(SettingsViewModel viewModel) =>
        new SettingsWindow(viewModel) { Owner = Owner }.ShowDialog();

    public static string? PickBackupFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha o backup para restaurar",
            Filter = "Backup da coleção (*.zip)|*.zip"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    /// <summary>Mostra os álbuns encontrados e devolve o escolhido (ou null se cancelar).</summary>
    public static AlbumMatch? PickAlbumMatch(IReadOnlyList<AlbumMatch> matches)
    {
        var window = new AlbumSearchWindow(matches) { Owner = Owner };
        return window.ShowDialog() == true ? window.Selected : null;
    }

    /// <summary>Onde salvar o resumo da coleção (imagem PNG ou PDF).</summary>
    public static string? PickShareFile(bool pdf)
    {
        var dialog = new SaveFileDialog
        {
            Title = pdf ? "Salvar resumo em PDF" : "Salvar resumo como imagem",
            Filter = pdf ? "Documento PDF (*.pdf)|*.pdf" : "Imagem PNG (*.png)|*.png",
            DefaultExt = pdf ? ".pdf" : ".png",
            FileName = $"spinvault-resumo-{DateTime.Now:yyyy-MM-dd}"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    /// <summary>Abre um arquivo com o programa padrão do Windows (visualizador de imagens, leitor de PDF…).</summary>
    public static void OpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Sem programa associado: o arquivo já foi salvo, então não é um erro para o usuário.
        }
    }

    public static bool Confirm(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    public static void Info(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string title, string message) =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public static string? PickExcelFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha a planilha da coleção",
            Filter = "Planilhas do Excel (*.xlsx)|*.xlsx"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    /// <summary>Planilha do Excel ou CSV (inclusive o exportado pelo Discogs).</summary>
    public static string? PickImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha a planilha ou o CSV da coleção",
            Filter = "Planilha ou CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Planilhas do Excel (*.xlsx)|*.xlsx|CSV / Discogs (*.csv)|*.csv"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public static string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolha a capa do álbum",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
}
