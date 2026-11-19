using System.Windows;
using System.Windows.Input;
using ColecaoMidias.Services;
using ColecaoMidias.ViewModels;

namespace ColecaoMidias.Views;

public partial class AlbumEditWindow : Window
{
    private readonly AlbumEditViewModel _viewModel;

    public AlbumEditWindow(AlbumEditViewModel viewModel)
    {
        InitializeComponent();

        // Com os campos novos a janela cresce: nunca passa da área útil da tela (o conteúdo rola).
        MaxHeight = SystemParameters.WorkArea.Height - 12;

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += saved => DialogResult = saved;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
    }

    /// <summary>Arrastar e soltar: um arquivo de imagem do PC ou uma imagem/link vindo do navegador.</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            _viewModel.SetCoverFile(files[0]);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.UnicodeText) &&
            e.Data.GetData(DataFormats.UnicodeText) is string text &&
            text.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.UseImageUrl(text);
        }
    }

    /// <summary>Enter no campo do álbum faz a busca de dados (em vez de salvar).</summary>
    private void OnTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        _viewModel.LookupAlbumCommand.Execute(null);
    }

    /// <summary>Enter no campo "nova lista" cria a lista (em vez de salvar o álbum).</summary>
    private void OnNewTagKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        _viewModel.AddTagCommand.Execute(null);
    }

    /// <summary>Enter no campo do link baixa a imagem (e não aciona o botão Salvar da janela).</summary>
    private void OnUrlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        _viewModel.DownloadFromUrlCommand.Execute(null);
    }
}
