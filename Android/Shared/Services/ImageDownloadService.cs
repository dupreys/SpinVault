using System.Net.Http.Headers;
using System.Text;

namespace ColecaoMidias.Services;

/// <summary>
/// Baixa uma imagem a partir de um link colado pelo usuário (ex.: "Copiar endereço da imagem" no Google).
/// Aceita também links de resultado do Google/Bing Imagens e imagens embutidas no próprio link (data:image).
/// </summary>
public sealed class ImageDownloadService : IDisposable
{
    private const long MaxBytes = 20 * 1024 * 1024;   // 20 MB: mais que isso não é uma capa

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public ImageDownloadService() : this(CreateClient(), ownsClient: true) { }

    /// <summary>Para testes: permite injetar um HttpClient falso.</summary>
    public ImageDownloadService(HttpClient http, bool ownsClient = false)
    {
        _http = http;
        _ownsClient = ownsClient;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Alguns sites recusam downloads sem cara de navegador.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg,image/png,image/gif,image/*;q=0.8,*/*;q=0.5");
        return client;
    }

    public async Task<CoverFetchResult> DownloadAsync(string input, CancellationToken ct = default)
    {
        var address = NormalizeInput(input);
        if (address is null)
            return new CoverFetchResult(null, Loc.T("Cole um endereço de imagem que comece com http:// ou https://."));

        byte[] data;
        try
        {
            if (address.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                data = DecodeDataUri(address);
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, address);
                var origin = new Uri(address).GetLeftPart(UriPartial.Authority) + "/";
                request.Headers.Referrer = new Uri(origin);   // ajuda contra bloqueio de "hotlink"

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                    return new CoverFetchResult(null,
                        Loc.F("O site recusou o download ({0}). Salve a imagem no PC e use \"Escolher capa…\".", (int)response.StatusCode));

                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                    return new CoverFetchResult(null,
                        Loc.T("Esse link é de uma página, não de uma imagem. Clique com o botão direito na imagem e escolha \"Copiar endereço da imagem\"."));

                if (response.Content.Headers.ContentLength > MaxBytes)
                    return new CoverFetchResult(null, Loc.T("A imagem é grande demais (limite de 20 MB)."));

                data = await ReadLimitedAsync(response, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new CoverFetchResult(null, Loc.T("Não foi possível acessar esse endereço. Verifique o link e a internet."), NetworkError: true);
        }
        catch (TaskCanceledException)
        {
            return new CoverFetchResult(null, Loc.T("O site demorou demais para responder."), NetworkError: true);
        }
        catch (InvalidDataException)
        {
            return new CoverFetchResult(null, Loc.T("A imagem é grande demais (limite de 20 MB)."));
        }
        catch (FormatException)
        {
            return new CoverFetchResult(null, Loc.T("O link da imagem está incompleto ou inválido."));
        }

        if (ExtensionFor(data) is null)
            return new CoverFetchResult(null, Loc.T("O link não parece ser de uma imagem válida (use JPG, PNG, GIF, BMP ou WebP)."));

        return new CoverFetchResult(data, Loc.T("Imagem baixada."));
    }

    // ---------- Endereço ----------

    /// <summary>
    /// Limpa o que o usuário colou e devolve um endereço http(s) ou data:image utilizável (ou null).
    /// Links de resultado do Google/Bing Imagens carregam o endereço real da foto dentro do parâmetro.
    /// </summary>
    public static string? NormalizeInput(string? input)
    {
        var text = (input ?? "").Trim().Trim('"', '\'', '<', '>');
        if (text.Length == 0)
            return null;

        if (text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return text;

        if (!text.Contains("://", StringComparison.Ordinal) && text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        var query = ParseQuery(uri.Query);
        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("google.") && uri.AbsolutePath.StartsWith("/imgres", StringComparison.OrdinalIgnoreCase)
                                     && query.TryGetValue("imgurl", out var googleImage))
            return NormalizeInput(googleImage);

        if (host.EndsWith("bing.com", StringComparison.Ordinal) && query.TryGetValue("mediaurl", out var bingImage))
            return NormalizeInput(bingImage);

        return uri.AbsoluteUri;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index <= 0)
                continue;

            var key = Uri.UnescapeDataString(pair[..index]);
            var value = Uri.UnescapeDataString(pair[(index + 1)..].Replace('+', ' '));
            result.TryAdd(key, value);
        }
        return result;
    }

    private static byte[] DecodeDataUri(string uri)
    {
        var comma = uri.IndexOf(',');
        if (comma < 0 || !uri[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("data URI sem base64");

        // Links copiados de páginas às vezes trazem quebras de linha ou espaços no meio.
        var base64 = uri[(comma + 1)..].Replace("\r", "").Replace("\n", "").Replace(" ", "");
        var data = Convert.FromBase64String(Uri.UnescapeDataString(base64));
        if (data.Length > MaxBytes)
            throw new InvalidDataException();
        return data;
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                throw new InvalidDataException();
        }
        return buffer.ToArray();
    }

    // ---------- Tipo da imagem (pelos primeiros bytes, sem confiar na extensão do link) ----------

    /// <summary>".jpg", ".png", ".gif", ".bmp", ".webp" ou null se não for imagem conhecida.</summary>
    public static string? ExtensionFor(byte[] d)
    {
        if (d.Length < 12)
            return null;
        if (d[0] == 0xFF && d[1] == 0xD8)
            return ".jpg";
        if (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47)
            return ".png";
        if (d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46)
            return ".gif";
        if (d[0] == 0x42 && d[1] == 0x4D)
            return ".bmp";
        if (d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46 &&
            d[8] == 0x57 && d[9] == 0x45 && d[10] == 0x42 && d[11] == 0x50)
            return ".webp";
        return null;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
