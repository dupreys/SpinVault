using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColecaoMidias.ViewModels;

/// <summary>Baixa as capas de vários álbuns, um de cada vez, mostrando o progresso.</summary>
public partial class CoverDownloadViewModel : ObservableObject
{
    private readonly AlbumRepository _repository;
    private readonly List<Album> _albums;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private int total;
    [ObservableProperty] private int done;
    [ObservableProperty] private int found;
    [ObservableProperty] private int missing;
    [ObservableProperty] private string statusText = "Preparando…";
    [ObservableProperty] private string currentItem = "";
    [ObservableProperty] private bool isRunning = true;
    [ObservableProperty] private string closeButtonText = "Cancelar";

    /// <summary>Pelo menos uma capa foi baixada (a coleção precisa ser recarregada).</summary>
    public bool Changed { get; private set; }

    public CoverDownloadViewModel(AlbumRepository repository, IEnumerable<Album> albums)
    {
        _repository = repository;
        _albums = albums.ToList();
        Total = _albums.Count;
    }

    public void Cancel() => _cts.Cancel();

    public async Task RunAsync()
    {
        using var service = new CoverFetchService();

        // O mesmo álbum em vinil e em CD usa a mesma imagem, sem consultar a internet de novo.
        var cache = new Dictionary<string, byte[]?>();
        var networkErrors = 0;

        try
        {
            foreach (var album in _albums)
            {
                _cts.Token.ThrowIfCancellationRequested();

                StatusText = $"Buscando {Done + 1} de {Total}";
                CurrentItem = $"{album.Artist} — {album.Title}";

                var key = $"{album.Artist.Trim().ToLowerInvariant()}|{album.Title.Trim().ToLowerInvariant()}";
                if (!cache.TryGetValue(key, out var data))
                {
                    var result = await service.FindCoverAsync(album.Artist, album.Title, _cts.Token);
                    data = result.Data;
                    cache[key] = data;

                    networkErrors = result.NetworkError ? networkErrors + 1 : 0;
                    if (networkErrors >= 3)
                    {
                        StatusText = "Sem conexão com o serviço de capas. Verifique a internet e tente de novo.";
                        return;
                    }
                }

                if (data is not null)
                {
                    album.CoverFile = CoverService.SaveBytes(data, CoverFetchService.ExtensionFor(data));
                    _repository.Update(album);
                    Found++;
                    Changed = true;
                }
                else
                {
                    Missing++;
                }

                Done++;
            }

            StatusText = Found == 0
                ? "Nenhuma capa encontrada."
                : $"Concluído! {Found} capas baixadas.";
            if (Missing > 0 && Found > 0)
                StatusText += $" {Missing} não foram encontradas.";
        }
        catch (OperationCanceledException)
        {
            StatusText = Found > 0 ? $"Cancelado. {Found} capas já foram salvas." : "Cancelado.";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            CurrentItem = "";
            CloseButtonText = "Fechar";
        }
    }
}
