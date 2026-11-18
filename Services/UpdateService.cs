using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ColecaoMidias.Services;

public sealed record UpdateInfo(string Version, string TagName, string PageUrl, string? DownloadUrl, string Notes);

/// <param name="Available">Versão mais nova encontrada (null se não há).</param>
/// <param name="Error">Erro de rede/serviço (null se a consulta funcionou).</param>
public sealed record UpdateCheckResult(UpdateInfo? Available, string Message, string? Error = null)
{
    public bool Failed => Error is not null;
}

/// <summary>Consulta a última Release publicada no GitHub e diz se existe versão mais nova que a instalada.</summary>
public sealed class UpdateService : IDisposable
{
    private const string Placeholder = "SEU-USUARIO";

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public string? Repository { get; }
    public Version Current { get; }

    public UpdateService(string? repository, Version current, HttpClient? http = null)
    {
        Repository = repository;
        Current = current;
        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpinVault", Format(current)));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>Cria o serviço com a versão e o repositório gravados no próprio programa (o GitHub Actions preenche).</summary>
    public static UpdateService Create()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var repository = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "UpdateRepository")?.Value;

        var text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
        TryParseVersion(text, out var version);
        return new UpdateService(repository, version);
    }

    /// <summary>Só consulta quando o repositório foi configurado (formato "dono/repo", sem o texto de exemplo).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Repository) && Repository.Contains('/') && !Repository.Contains(Placeholder, StringComparison.OrdinalIgnoreCase);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new UpdateCheckResult(null, Loc.T("A verificação de atualização ainda não foi configurada neste programa (repositório do GitHub não informado)."), "not-configured");

        try
        {
            using var response = await _http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(null, Loc.T("Nenhuma versão foi publicada ainda no GitHub."));
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new UpdateCheckResult(null, Loc.T("O GitHub limitou as consultas por enquanto. Tente de novo mais tarde."), "rate-limit");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var tag = Text(root, "tag_name");
            if (!TryParseVersion(tag, out var latest))
                return new UpdateCheckResult(null, Loc.F("Não entendi o número da última versão (\"{0}\").", tag), "bad-version");

            if (latest <= Normalize(Current))
                return new UpdateCheckResult(null, Loc.F("Você está com a versão mais recente ({0}).", Format(Current)));

            string? download = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = Text(asset, "name");
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        download = Text(asset, "browser_download_url");
                        break;
                    }
                }
            }

            var info = new UpdateInfo(Format(latest), tag, Text(root, "html_url"), download is { Length: > 0 } ? download : null, Text(root, "body"));
            return new UpdateCheckResult(info, Loc.F("Nova versão disponível: {0} (você tem {1}).", info.Version, Format(Current)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new UpdateCheckResult(null, Loc.T("Não consegui consultar o GitHub (sem internet?)."), ex.Message);
        }
    }

    // ---------- Versões ----------

    /// <summary>"v1.2.0", "1.2.0+abc123" ou "1.2.0-beta" → 1.2.0. Ignora sufixos.</summary>
    internal static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var core = text.Trim().TrimStart('v', 'V');
        var cut = core.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0)
            core = core[..cut];

        if (!Version.TryParse(core, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    private static Version Normalize(Version v) => new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    internal static string Format(Version v)
    {
        var n = Normalize(v);
        return $"{n.Major}.{n.Minor}.{n.Build}";
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
