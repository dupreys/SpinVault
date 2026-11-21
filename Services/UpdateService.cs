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
    private readonly string _tagPrefix;
    private readonly string _assetSuffix;
    private readonly string? _assetContains;

    public string? Repository { get; }
    public Version Current { get; }

    /// <param name="tagPrefix">O repositório tem duas famílias de releases: "v1.3.0" (Windows) e "android-v1.1.1" (Android). Só as do prefixo certo contam.</param>
    /// <param name="assetSuffix">Terminação do arquivo para baixar (".exe" no Windows, ".apk" no Android).</param>
    /// <param name="assetContains">Texto que o nome do arquivo precisa ter ("Setup" para o instalador do Windows).</param>
    public UpdateService(string? repository, Version current, HttpClient? http = null,
                         string tagPrefix = "v", string assetSuffix = ".exe", string? assetContains = "Setup")
    {
        Repository = repository;
        Current = current;
        _tagPrefix = tagPrefix;
        _assetSuffix = assetSuffix;
        _assetContains = assetContains;
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
            // Não dá para usar "releases/latest": ela devolve a release mais recente de qualquer família (Windows ou Android).
            using var response = await _http.GetAsync($"https://api.github.com/repos/{Repository}/releases?per_page=30", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new UpdateCheckResult(null, Loc.T("Nenhuma versão foi publicada ainda no GitHub."));
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new UpdateCheckResult(null, Loc.T("O GitHub limitou as consultas por enquanto. Tente de novo mais tarde."), "rate-limit");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            JsonElement? release = null;
            var latest = new Version(0, 0, 0);
            var tag = "";
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in doc.RootElement.EnumerateArray())
                {
                    if (Flag(candidate, "draft") || Flag(candidate, "prerelease"))
                        continue;
                    var candidateTag = Text(candidate, "tag_name");
                    if (!candidateTag.StartsWith(_tagPrefix, StringComparison.OrdinalIgnoreCase) ||
                        !TryParseVersion(candidateTag[_tagPrefix.Length..], out var candidateVersion))
                        continue;
                    if (release is null || candidateVersion > latest)
                    {
                        release = candidate;
                        latest = candidateVersion;
                        tag = candidateTag;
                    }
                }
            }

            if (release is not { } root)
                return new UpdateCheckResult(null, Loc.T("Nenhuma versão foi publicada ainda no GitHub."));

            if (latest <= Normalize(Current))
                return new UpdateCheckResult(null, Loc.F("Você está com a versão mais recente ({0}).", Format(Current)));

            string? download = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = Text(asset, "name");
                    if (name.EndsWith(_assetSuffix, StringComparison.OrdinalIgnoreCase) &&
                        (_assetContains is null || name.Contains(_assetContains, StringComparison.OrdinalIgnoreCase)))
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

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}
