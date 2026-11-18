using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpinVaultMobile.Cloud;

/// <summary>A sessão do Google expirou ou foi revogada: o usuário precisa entrar de novo.</summary>
public sealed class AuthExpiredException : Exception
{
    public AuthExpiredException() : base("Sessão do Google expirada.") { }
}

/// <summary>
/// Login com a conta Google (OAuth 2.0 com PKCE, pelo navegador do celular).
/// Pede só acesso à pasta oculta do app no Google Drive e ao e-mail da conta.
/// </summary>
public static class GoogleAuth
{
#if DEBUG
    // ID do cliente Android assinado com a chave de depuração do Visual Studio.
    private const string ClientCode = "108914129021-hbc426r2besd5j2l6emn7hs91vkg4f9o";
#else
    // ID do cliente Android assinado com a chave de Release (spinvault.keystore), usada no APK publicado.
    private const string ClientCode = "108914129021-ara0ld7pjv7aokevi20ldp14vq29tbqc";
#endif
    private const string ClientId = ClientCode + ".apps.googleusercontent.com";
    private const string Scheme = "com.googleusercontent.apps." + ClientCode;
    private const string Scopes = "openid email https://www.googleapis.com/auth/drive.appdata";
    private const string RefreshKey = "google_refresh_token";
    private const string EmailKey = "google_email";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static string? _accessToken;
    private static DateTime _expiresAt;

    public static string RedirectUri => Scheme + ":/oauth2redirect";
    public static string CallbackScheme => Scheme;

    public static bool IsSignedIn => Preferences.Get("google_signed_in", false);
    public static string Email => Preferences.Get(EmailKey, "");

    /// <summary>Abre o login do Google. Devolve false se a pessoa cancelou.</summary>
    public static async Task<bool> SignInAsync()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));

        var url = "https://accounts.google.com/o/oauth2/v2/auth" +
                  "?client_id=" + Uri.EscapeDataString(ClientId) +
                  "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
                  "&response_type=code" +
                  "&scope=" + Uri.EscapeDataString(Scopes) +
                  "&code_challenge=" + challenge +
                  "&code_challenge_method=S256" +
                  "&state=" + state +
                  "&access_type=offline&prompt=consent";

        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = new Uri(url),
                CallbackUrl = new Uri(RedirectUri),
            });
        }
        catch (TaskCanceledException)
        {
            return false;
        }

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            result.Properties.TryGetValue("error", out var error);
            throw new InvalidOperationException(error ?? "O Google não devolveu o código de acesso.");
        }
        if (!result.Properties.TryGetValue("state", out var returned) || returned != state)
            throw new InvalidOperationException("Resposta de login inválida.");

        var json = await PostFormAsync(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code",
        });

        var root = json.RootElement;
        _accessToken = root.GetProperty("access_token").GetString();
        _expiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 60);

        if (!root.TryGetProperty("refresh_token", out var refresh) || string.IsNullOrEmpty(refresh.GetString()))
            throw new InvalidOperationException("O Google não devolveu a permissão de acesso contínuo. Tente entrar de novo.");

        await SecureStorage.Default.SetAsync(RefreshKey, refresh.GetString()!);
        Preferences.Set(EmailKey, EmailFromIdToken(root.TryGetProperty("id_token", out var id) ? id.GetString() : null));
        Preferences.Set("google_signed_in", true);
        return true;
    }

    public static void SignOut()
    {
        _accessToken = null;
        SecureStorage.Default.Remove(RefreshKey);
        Preferences.Remove(EmailKey);
        Preferences.Set("google_signed_in", false);
        Preferences.Remove("sync_local_fp");
        Preferences.Remove("sync_remote_mod");
        Preferences.Remove("sync_last");
    }

    /// <summary>Token de acesso válido (renova sozinho com a permissão guardada).</summary>
    public static async Task<string> GetAccessTokenAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _accessToken is not null && DateTime.UtcNow < _expiresAt)
            return _accessToken;

        string? refresh = null;
        try { refresh = await SecureStorage.Default.GetAsync(RefreshKey); }
        catch (Exception) { }
        if (string.IsNullOrEmpty(refresh))
        {
            SignOut();
            throw new AuthExpiredException();
        }

        try
        {
            var json = await PostFormAsync(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["refresh_token"] = refresh,
                ["grant_type"] = "refresh_token",
            });
            _accessToken = json.RootElement.GetProperty("access_token").GetString();
            _expiresAt = DateTime.UtcNow.AddSeconds(json.RootElement.GetProperty("expires_in").GetInt32() - 60);
            return _accessToken!;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            SignOut();
            throw new AuthExpiredException();
        }
    }

    private static async Task<JsonDocument> PostFormAsync(Dictionary<string, string> form)
    {
        using var response = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google: {body}");
        return JsonDocument.Parse(body);
    }

    private static string EmailFromIdToken(string? idToken)
    {
        try
        {
            var payload = idToken!.Split('.')[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.GetProperty("email").GetString() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
