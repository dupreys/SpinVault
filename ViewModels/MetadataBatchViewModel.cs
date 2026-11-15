using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColecaoMidias.ViewModels;

/// <summary>Completa a data de lançamento dos álbuns que estão sem ela, buscando na internet um de cada vez.</summary>
public partial class MetadataBatchViewModel : ObservableObject
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

    /// <summary>Pelo menos um álbum foi atualizado (a coleção precisa ser recarregada).</summary>
    public bool Changed { get; private set; }

    public MetadataBatchViewModel(AlbumRepository repository, IEnumerable<Album> albums)
    {
        _repository = repository;
        _albums = albums.ToList();
        Total = _albums.Count;
    }

    public void Cancel() => _cts.Cancel();

    public async Task RunAsync()
    {
        using var service = new AlbumLookupService();

        // O mesmo álbum em vinil e em CD usa a mesma resposta, sem consultar a internet de novo.
        var cache = new Dictionary<string, DateTime?>();
        var networkErrors = 0;

        try
        {
            foreach (var album in _albums)
            {
                _cts.Token.ThrowIfCancellationRequested();

                StatusText = $"Buscando {Done + 1} de {Total}";
                CurrentItem = $"{album.Artist} — {album.Title}";

                var key = $"{album.Artist.Trim().ToLowerInvariant()}|{album.Title.Trim().ToLowerInvariant()}";
                if (!cache.TryGetValue(key, out var date))
                {
                    var result = await service.SearchAsync(album.Title, album.Artist, _cts.Token);

                    networkErrors = result.NetworkError ? networkErrors + 1 : 0;
                    if (networkErrors >= 3)
                    {
                        StatusText = "Sem conexão com o serviço de dados. Verifique a internet e tente de novo.";
                        return;
                    }

                    date = null;
                    if (AlbumLookupService.PickConfident(result.Matches, album.Artist) is { } match &&
                        Fmt.TryParseDate(match.ReleaseDateText, out var parsed))
                        date = parsed;

                    if (!result.NetworkError)
                        cache[key] = date;
                }

                if (date is { } release)
                {
                    album.ReleaseDate = release;
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
                ? "Nenhum lançamento encontrado."
                : $"Concluído! {Found} álbuns completados.";
            if (Missing > 0 && Found > 0)
                StatusText += $" {Missing} ficaram sem data (preencha à mão se quiser).";
        }
        catch (OperationCanceledException)
        {
            StatusText = Found > 0 ? $"Cancelado. {Found} álbuns já foram atualizados." : "Cancelado.";
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
