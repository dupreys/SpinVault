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
