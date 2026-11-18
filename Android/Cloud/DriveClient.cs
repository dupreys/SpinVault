using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SpinVaultMobile.Cloud;

/// <summary>Arquivo de backup guardado na pasta oculta do app no Google Drive.</summary>
public sealed record DriveFile(string Id, string Modified)
{
    public DateTime ModifiedLocal => DateTimeOffset.TryParse(Modified, out var d) ? d.LocalDateTime : DateTime.Now;
}

/// <summary>Chamadas mínimas à API do Google Drive (só a pasta "appDataFolder", invisível para o usuário).</summary>
public static class DriveClient
{
    private const string BackupName = "spinvault-backup.zip";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static async Task<DriveFile?> FindBackupAsync()
    {
        var url = "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder" +
                  "&q=" + Uri.EscapeDataString($"name = '{BackupName}' and trashed = false") +
                  "&orderBy=" + Uri.EscapeDataString("modifiedTime desc") +
                  "&fields=" + Uri.EscapeDataString("files(id,modifiedTime)");
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var file in doc.RootElement.GetProperty("files").EnumerateArray())
            return new DriveFile(file.GetProperty("id").GetString()!, file.GetProperty("modifiedTime").GetString()!);
        return null;
    }

    public static async Task<DriveFile> UploadAsync(string path, string? existingId)
    {
        var bytes = await File.ReadAllBytesAsync(path);

        HttpRequestMessage Build()
        {
            var zip = new ByteArrayContent(bytes);
            zip.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            if (existingId is not null)
            {
                return new HttpRequestMessage(HttpMethod.Patch,
                    $"https://www.googleapis.com/upload/drive/v3/files/{existingId}?uploadType=media&fields=id,modifiedTime")
                { Content = zip };
            }

            var meta = JsonSerializer.Serialize(new { name = BackupName, parents = new[] { "appDataFolder" } });
            var content = new MultipartContent("related", "spinvault-boundary")
            {
                new StringContent(meta, Encoding.UTF8, "application/json"),
                zip,
            };
            return new HttpRequestMessage(HttpMethod.Post,
                "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,modifiedTime")
            { Content = content };
        }

        using var response = await SendAsync(Build);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new DriveFile(doc.RootElement.GetProperty("id").GetString()!, doc.RootElement.GetProperty("modifiedTime").GetString()!);
    }

    public static async Task DownloadAsync(string id, string destination)
    {
        using var response = await SendAsync(() =>
            new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{id}?alt=media"));
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output);
    }

    // Envia a requisição com o token; se o Google disser que o token venceu, renova e tenta uma vez mais.
    private static async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> build)
    {
        for (var attempt = 0; ; attempt++)
        {
            var request = build();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GoogleAuth.GetAccessTokenAsync(attempt > 0));
            var response = await Http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                response.Dispose();
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                response.Dispose();
                throw new HttpRequestException($"Google Drive ({(int)response.StatusCode}): {body}");
            }
            return response;
        }
    }
}
