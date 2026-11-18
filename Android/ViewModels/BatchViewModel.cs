using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpinVaultMobile.ViewModels;

/// <summary>Baixa capas ou completa datas de lançamento de vários álbuns, um de cada vez.</summary>
public sealed partial class BatchViewModel : ObservableObject
{
    private readonly bool _covers;
    private readonly List<Album> _albums;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private int total;
    [ObservableProperty] private int done;
    [ObservableProperty] private int found;
    [ObservableProperty] private int missing;
    [ObservableProperty] private double progress;
    [ObservableProperty] private string statusText = Loc.T("Preparando…");
    [ObservableProperty] private string currentItem = "";
    [ObservableProperty] private bool isRunning = true;
    [ObservableProperty] private string closeButtonText = Loc.T("Cancelar");

    public BatchViewModel(bool covers, IEnumerable<Album> albums)
    {
        _covers = covers;
        _albums = albums.ToList();
        Total = _albums.Count;
    }

    public string Title => _covers ? Loc.T("Baixar capas") : Loc.T("Completar dados");

    public void Cancel() => _cts.Cancel();

    public Task RunAsync() => _covers ? RunCoversAsync() : RunDatesAsync();

    private void Step(Album album)
    {
        StatusText = Loc.F("Buscando {0} de {1}", Done + 1, Total);
        CurrentItem = $"{album.Artist} — {album.Title}";
    }

    private void Advance()
    {
        Done++;
        Progress = Total == 0 ? 1 : (double)Done / Total;
    }

    private static string KeyOf(Album a) => $"{a.Artist.Trim().ToLowerInvariant()}|{a.Title.Trim().ToLowerInvariant()}";

    private async Task RunCoversAsync()
    {
        using var service = new CoverFetchService();
        var repo = AppData.Repo;
        var cache = new Dictionary<string, (byte[] Data, string Ext)?>();
        var networkErrors = 0;

        try
        {
            foreach (var album in _albums)
            {
                _cts.Token.ThrowIfCancellationRequested();
                Step(album);

                var key = KeyOf(album);
                if (!cache.TryGetValue(key, out var cover))
                {
                    var result = await service.FindCoverAsync(album.Artist, album.Title, _cts.Token);
                    networkErrors = result.NetworkError ? networkErrors + 1 : 0;
                    if (networkErrors >= 3)
                    {
                        StatusText = Loc.T("Sem conexão com o serviço de capas. Verifique a internet e tente de novo.");
                        return;
                    }

                    cover = null;
                    if (result.Found)
                    {
                        var data = result.Data!;
                        var shrunk = await Task.Run(() => ImageHelper.Shrink(data, CoverFetchService.ExtensionFor(data)));
                        cover = shrunk;
                    }
                    if (!result.NetworkError)
                        cache[key] = cover;
                }

                if (cover is { } c)
                {
                    album.CoverFile = CoverService.SaveBytes(c.Data, c.Ext);
                    repo.Update(album);
                    Found++;
                }
                else
                {
                    Missing++;
                }

                Advance();
            }

            StatusText = Found == 0
                ? Loc.T("Nenhuma capa encontrada.")
                : Loc.F("Concluído! {0} capas baixadas.", Found);
            if (Missing > 0 && Found > 0)
                StatusText += Loc.F(" {0} não foram encontradas.", Missing);
        }
        catch (OperationCanceledException)
        {
            StatusText = Found > 0 ? Loc.F("Cancelado. {0} capas já foram salvas.", Found) : Loc.T("Cancelado.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Erro: {0}", ex.Message);
        }
        finally
        {
            Finish();
        }
    }

    private async Task RunDatesAsync()
    {
        using var service = new AlbumLookupService();
        var repo = AppData.Repo;
        var cache = new Dictionary<string, DateTime?>();
        var networkErrors = 0;

        try
        {
            foreach (var album in _albums)
            {
                _cts.Token.ThrowIfCancellationRequested();
                Step(album);

                var key = KeyOf(album);
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
                    repo.Update(album);
                    Found++;
                }
                else
                {
                    Missing++;
                }

                Advance();
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
            Finish();
        }
    }

    private void Finish()
    {
        IsRunning = false;
        CurrentItem = "";
        CloseButtonText = Loc.T("Fechar");
    }
}
