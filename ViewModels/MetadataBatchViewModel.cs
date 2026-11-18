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
    [ObservableProperty] private string statusText = Loc.T("Preparando…");
    [ObservableProperty] private string currentItem = "";
    [ObservableProperty] private bool isRunning = true;
    [ObservableProperty] private string closeButtonText = Loc.T("Cancelar");

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

                StatusText = Loc.F("Buscando {0} de {1}", Done + 1, Total);
                CurrentItem = $"{album.Artist} — {album.Title}";

                var key = $"{album.Artist.Trim().ToLowerInvariant()}|{album.Title.Trim().ToLowerInvariant()}";
                if (!cache.TryGetValue(key, out var date))
                {
                    var result = await service.SearchAsync(album.Title, album.Artist, _cts.Token);

                    networkErrors = result.NetworkError ? networkErrors + 1 : 0;
                    if (networkErrors >= 3)
                    {
                        StatusText = Loc.T("Sem conexão com o serviço de dados. Verifique a internet e tente de novo.");
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
                ? Loc.T("Nenhum lançamento encontrado.")
                : Loc.F("Concluído! {0} álbuns completados.", Found);
            if (Missing > 0 && Found > 0)
                StatusText += Loc.F(" {0} ficaram sem data (preencha à mão se quiser).", Missing);
        }
        catch (OperationCanceledException)
        {
            StatusText = Found > 0 ? Loc.F("Cancelado. {0} álbuns já foram atualizados.", Found) : Loc.T("Cancelado.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Erro: {0}", ex.Message);
        }
        finally
        {
            IsRunning = false;
            CurrentItem = "";
            CloseButtonText = Loc.T("Fechar");
        }
    }
}
