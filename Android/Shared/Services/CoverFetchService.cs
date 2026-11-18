using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColecaoMidias.Services;

/// <summary>Resultado de uma busca de capa. Data é a imagem (JPG/PNG) quando encontrada.</summary>
public sealed record CoverFetchResult(byte[]? Data, string Message, bool NetworkError = false)
{
    public bool Found => Data is { Length: > 0 };
}

/// <summary>
/// Busca a capa de um álbum na internet:
/// 1) MusicBrainz (gratuito, sem chave) descobre qual é o álbum;
/// 2) Cover Art Archive baixa a imagem da capa dele.
/// Respeita o limite do MusicBrainz de 1 consulta por segundo.
/// </summary>
public sealed class CoverFetchService : IDisposable
{
    private const string MusicBrainzSearch = "https://musicbrainz.org/ws/2/release-group/";
    private const string CoverArtArchive = "https://coverartarchive.org/release-group/";
    private const int MinScore = 75;          // pontuação mínima (0–100) do MusicBrainz para aceitar o resultado
    private const int MaxCandidates = 3;      // quantos resultados tentar antes de desistir

    // Artistas "genéricos" (coletâneas): buscar só pelo título.
    private static readonly HashSet<string> GenericArtists = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coletânia", "Coletania", "Coletânea", "Coletanea", "Various", "Various Artists", "Vários", "Varios",
        "Vários Artistas", "Diversos", "VA"
    };

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly TimeSpan _minInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastMusicBrainzCall = DateTime.MinValue;

    public CoverFetchService() : this(CreateClient(), TimeSpan.FromMilliseconds(1100), ownsClient: true) { }

    /// <summary>Para testes: permite injetar um HttpClient falso e um intervalo menor.</summary>
    public CoverFetchService(HttpClient http, TimeSpan minInterval, bool ownsClient = false)
    {
        _http = http;
        _minInterval = minInterval;
        _ownsClient = ownsClient;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        // O MusicBrainz exige um User-Agent que identifique o programa.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ColecaoMidias", "1.0"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(catalogo-pessoal-de-midias)"));
        return client;
    }

    public async Task<CoverFetchResult> FindCoverAsync(string artist, string title, CancellationToken ct = default)
    {
        title = title.Trim();
        artist = artist.Trim();
        if (title.Length == 0)
            return new CoverFetchResult(null, Loc.T("Informe o nome do álbum."));

        var sawCandidates = false;

        try
        {
            foreach (var variant in TitleVariants(title))
            {
                var candidates = await SearchAsync(artist, variant, ct);
                if (candidates.Count == 0)
                    continue;

                sawCandidates = true;
                foreach (var candidate in candidates.Take(MaxCandidates))
                {
                    var image = await DownloadCoverAsync(candidate, ct);
                    if (image is not null)
                        return new CoverFetchResult(image, Loc.T("Capa encontrada."));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new CoverFetchResult(null, Loc.F("Sem conexão com o serviço de capas ({0}).", ex.StatusCode?.ToString() ?? ex.Message), NetworkError: true);
        }
        catch (TaskCanceledException)
        {
            return new CoverFetchResult(null, Loc.T("O serviço de capas demorou demais para responder."), NetworkError: true);
        }
        catch (JsonException)
        {
            return new CoverFetchResult(null, Loc.T("Resposta inesperada do serviço de capas."));
        }

        return new CoverFetchResult(null, sawCandidates
            ? Loc.T("O álbum foi encontrado, mas não tem capa cadastrada.")
            : Loc.T("Álbum não encontrado no MusicBrainz."));
    }

    // ---------- MusicBrainz ----------

    private sealed record Candidate(string Id, int Score, string Title);

    private async Task<List<Candidate>> SearchAsync(string artist, string title, CancellationToken ct)
    {
        var query = $"releasegroup:\"{Escape(title)}\"";
        if (artist.Length > 0 && !GenericArtists.Contains(artist))
            query += $" AND artist:\"{Escape(artist)}\"";

        var url = $"{MusicBrainzSearch}?query={Uri.EscapeDataString(query)}&fmt=json&limit=6";

        await WaitTurnAsync(ct);
        using var response = await _http.GetAsync(url, ct);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("limite de consultas", null, response.StatusCode);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<Candidate>();
        if (!doc.RootElement.TryGetProperty("release-groups", out var groups))
            return list;

        foreach (var group in groups.EnumerateArray())
        {
            var id = group.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            if (string.IsNullOrEmpty(id))
                continue;

            var score = group.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetInt32(out var s) ? s : 0;
            var name = group.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? "" : "";

            if (score >= MinScore)
                list.Add(new Candidate(id, score, name));
        }

        // Melhor pontuação primeiro; em empate, prefere o título idêntico ao pedido.
        var wanted = Simplify(title);
        return list
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => Simplify(c.Title) == wanted)
            .ToList();
    }

    /// <summary>Garante 1 consulta por segundo ao MusicBrainz, mesmo com várias chamadas seguidas.</summary>
    private async Task WaitTurnAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = _lastMusicBrainzCall + _minInterval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);

            _lastMusicBrainzCall = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---------- Cover Art Archive ----------

    /// <summary>Baixa a capa de um álbum já identificado (id do MusicBrainz). Devolve null se não houver capa ou der erro.</summary>
    public async Task<byte[]?> FindCoverByIdAsync(string releaseGroupId, CancellationToken ct = default)
    {
        try
        {
            return await DownloadCoverAsync(new Candidate(releaseGroupId, 100, ""), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    private async Task<byte[]?> DownloadCoverAsync(Candidate candidate, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"{CoverArtArchive}{candidate.Id}/front-500", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;                        // esse álbum não tem capa: tenta o próximo candidato

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return LooksLikeImage(bytes) ? bytes : null;
    }

    // ---------- Utilitários ----------

    /// <summary>Extensão certa para salvar a imagem (".png" ou ".jpg").</summary>
    public static string ExtensionFor(byte[] data) =>
        data.Length > 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 ? ".png" : ".jpg";

    private static bool LooksLikeImage(byte[] data) =>
        data.Length > 1000 &&
        ((data[0] == 0xFF && data[1] == 0xD8) ||                                           // JPEG
         (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47));      // PNG

    /// <summary>"Close to the edge (Vinil)" → também tenta "Close to the edge".</summary>
    internal static IEnumerable<string> TitleVariants(string title)
    {
        yield return title;

        var withoutBrackets = Regex.Replace(title, @"\s*[\(\[][^\)\]]*[\)\]]", "").Trim();
        if (withoutBrackets.Length > 0 && !withoutBrackets.Equals(title, StringComparison.OrdinalIgnoreCase))
            yield return withoutBrackets;
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Simplify(string text) =>
        new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public void Dispose()
    {
        _gate.Dispose();
        if (_ownsClient)
            _http.Dispose();
    }
}
