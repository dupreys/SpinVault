using ColecaoMidias.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ColecaoMidias.Services;

/// <summary>Um álbum encontrado na internet.</summary>
/// <param name="ReleaseDateText">Data no formato do app: "dd/MM/aaaa" quando a data completa é conhecida; senão só o ano.</param>
public sealed record AlbumMatch(string Id, string Title, string Artist, string ReleaseDateText, string Kind, int Score)
{
    public bool HasDate => ReleaseDateText.Length > 0;

    /// <summary>Texto da linha na lista de escolha, ex.: "A-HA · 1985 · Álbum".</summary>
    public string Subtitle =>
        string.Join("  ·  ", new[] { Artist, HasDate ? ReleaseDateText : Loc.T("sem data"), Kind }.Where(x => x.Length > 0));
}

public sealed record AlbumLookupResult(IReadOnlyList<AlbumMatch> Matches, string Message, bool NetworkError = false);

/// <summary>
/// Descobre artista e data de lançamento de um álbum só pelo nome (e, se houver, pelo artista),
/// usando o MusicBrainz — base aberta e gratuita, sem necessidade de cadastro.
/// </summary>
public sealed class AlbumLookupService : IDisposable
{
    private const string Endpoint = "https://musicbrainz.org/ws/2/release-group/";
    private const int MinScore = 70;
    private const int MaxResults = 10;

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly TimeSpan _minInterval;
    private DateTime _lastCall = DateTime.MinValue;

    public AlbumLookupService() : this(CreateClient(), TimeSpan.FromMilliseconds(1100), ownsClient: true) { }

    /// <summary>Para testes: permite injetar um HttpClient falso e um intervalo menor.</summary>
    public AlbumLookupService(HttpClient http, TimeSpan minInterval, bool ownsClient = false)
    {
        _http = http;
        _minInterval = minInterval;
        _ownsClient = ownsClient;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpinVault", "1.2"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(catalogo-pessoal-de-midias)"));
        return client;
    }

    public async Task<AlbumLookupResult> SearchAsync(string title, string artist = "", CancellationToken ct = default)
    {
        title = CleanTitle(title);
        artist = artist.Trim();

        if (title.Length == 0)
            return new AlbumLookupResult(Array.Empty<AlbumMatch>(), Loc.T("Digite o nome do álbum para buscar."));

        try
        {
            // 1ª tentativa: título exato (frase). 2ª: as palavras soltas, para títulos escritos de forma diferente.
            var queries = new List<string> { Build($"releasegroup:\"{Escape(title)}\"", artist) };
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(EscapeWord).ToList();
            if (words.Count > 1)
                queries.Add(Build($"releasegroup:({string.Join(" AND ", words)})", artist));

            foreach (var query in queries)
            {
                var matches = await RunQueryAsync(query, ct);
                if (matches.Count > 0)
                    return new AlbumLookupResult(matches, Loc.F("{0} resultado(s).", matches.Count));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new AlbumLookupResult(Array.Empty<AlbumMatch>(),
                Loc.F("Sem conexão com o serviço de dados ({0}). Preencha manualmente.", ex.StatusCode?.ToString() ?? ex.Message), NetworkError: true);
        }
        catch (TaskCanceledException)
        {
            return new AlbumLookupResult(Array.Empty<AlbumMatch>(),
                Loc.T("O serviço de dados demorou demais para responder. Tente de novo ou preencha manualmente."), NetworkError: true);
        }
        catch (JsonException)
        {
            return new AlbumLookupResult(Array.Empty<AlbumMatch>(), Loc.T("Resposta inesperada do serviço de dados."));
        }

        return new AlbumLookupResult(Array.Empty<AlbumMatch>(), Loc.T("Não encontrei esse álbum. Preencha manualmente."));
    }

    private static string Build(string albumPart, string artist) =>
        artist.Length > 0 ? $"{albumPart} AND artist:\"{Escape(artist)}\"" : albumPart;

    private async Task<List<AlbumMatch>> RunQueryAsync(string query, CancellationToken ct)
    {
        // MusicBrainz permite 1 consulta por segundo.
        var wait = _lastCall + _minInterval - DateTime.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, ct);
        _lastCall = DateTime.UtcNow;

        var url = $"{Endpoint}?query={Uri.EscapeDataString(query)}&fmt=json&limit={MaxResults}";
        using var response = await _http.GetAsync(url, ct);

        if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("limite de consultas", null, response.StatusCode);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<AlbumMatch>();
        if (!doc.RootElement.TryGetProperty("release-groups", out var groups))
            return list;

        foreach (var group in groups.EnumerateArray())
        {
            var id = Text(group, "id");
            var title = Text(group, "title");
            var score = group.TryGetProperty("score", out var s) && s.TryGetInt32(out var sc) ? sc : 0;
            var artist = ArtistName(group);

            if (id.Length == 0 || title.Length == 0 || artist.Length == 0 || score < MinScore)
                continue;

            list.Add(new AlbumMatch(id, title, artist, FormatDate(Text(group, "first-release-date")), KindLabel(group), score));
        }

        // Melhor pontuação primeiro; álbuns "de estúdio" antes de ao vivo/coletâneas; depois o mais antigo.
        return list
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Kind == Loc.T("Álbum") ? 0 : 1)
            .ThenBy(m => SortableDate(m.ReleaseDateText))
            .ToList();
    }

    // ---------- Escolha automática (usada no preenchimento em lote) ----------

    /// <summary>
    /// Escolhe sozinho o resultado que dá para confiar: boa pontuação, com data e do mesmo artista.
    /// Devolve null quando há dúvida — melhor deixar em branco do que preencher errado.
    /// </summary>
    public static AlbumMatch? PickConfident(IReadOnlyList<AlbumMatch> matches, string artist)
    {
        var wanted = Simplify(artist);
        if (wanted.Length == 0)
            return null;

        return matches.FirstOrDefault(m =>
        {
            if (m.Score < 85 || !m.HasDate)
                return false;
            var found = Simplify(m.Artist);
            return found.Length > 0 && (found == wanted || found.Contains(wanted) || wanted.Contains(found));
        });
    }

    private static string Simplify(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(char.IsLetterOrDigit).ToArray());
    }

    // ---------- Conversões ----------

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string ArtistName(JsonElement group)
    {
        if (!group.TryGetProperty("artist-credit", out var credits) || credits.ValueKind != JsonValueKind.Array)
            return "";

        var builder = new StringBuilder();
        foreach (var credit in credits.EnumerateArray())
        {
            builder.Append(Text(credit, "name"));
            builder.Append(Text(credit, "joinphrase"));   // ex.: " & ", " feat. "
        }
        return builder.ToString().Trim();
    }

    /// <summary>"1985-06-01" → "01/06/1985" (ou "06/01/1985" em inglês); "1985-06" ou "1985" → "1985" (não inventa o dia).</summary>
    internal static string FormatDate(string raw)
    {
        raw = raw.Trim();
        if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var full))
            return Fmt.Date(full);

        return raw.Length >= 4 && int.TryParse(raw[..4], out var year) && year is >= 1900 and <= 2100
            ? year.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    private static DateTime SortableDate(string text)
    {
        if (text.Length > 4 && Fmt.TryParseDate(text, out var full))
            return full;
        return int.TryParse(text, out var year) ? new DateTime(year, 1, 1) : DateTime.MaxValue;
    }

    private static string KindLabel(JsonElement group)
    {
        var primary = Text(group, "primary-type") switch
        {
            "Album" => Loc.T("Álbum"),
            "Single" => Loc.T("Single"),
            "EP" => Loc.T("EP"),
            "Broadcast" => Loc.T("Transmissão"),
            "" => "",
            _ => Loc.T("Outro")
        };

        var extras = new List<string>();
        if (group.TryGetProperty("secondary-types", out var secondary) && secondary.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in secondary.EnumerateArray())
            {
                var label = item.GetString() switch
                {
                    "Compilation" => Loc.T("Coletânea"),
                    "Live" => Loc.T("Ao vivo"),
                    "Soundtrack" => Loc.T("Trilha sonora"),
                    "Remix" => Loc.T("Remix"),
                    "Demo" => Loc.T("Demo"),
                    "Interview" => Loc.T("Entrevista"),
                    "Spokenword" => Loc.T("Narrado"),
                    "Audiobook" => Loc.T("Audiolivro"),
                    _ => null
                };
                if (label is not null)
                    extras.Add(label);
            }
        }

        return string.Join(" · ", new[] { primary }.Concat(extras).Where(x => x.Length > 0));
    }

    // ---------- Texto ----------

    /// <summary>Tira sufixos que o usuário põe no cadastro, como "(Vinil)", "(CD)" ou "[Remaster]".</summary>
    internal static string CleanTitle(string title)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(title, @"\s*[\(\[]\s*(vinil|vinyl|lp|cd|cassete|cassette|fita|k7)\s*[\)\]]", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Caracteres especiais da busca Lucene, quando a palavra vai fora de aspas.
    private static string EscapeWord(string word)
    {
        var builder = new StringBuilder();
        foreach (var c in word)
        {
            if ("+-&|!(){}[]^\"~*?:\\/".Contains(c))
                builder.Append('\\');
            builder.Append(c);
        }
        return builder.ToString();
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
