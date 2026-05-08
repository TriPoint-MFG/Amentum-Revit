using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

/// <summary>
/// Optional enterprise endpoint integration for uploading exported geometry files.
///
/// Configure via environment variable:
///   AMENTUM_UPLOAD_URL — full URL to the upload endpoint
///                        (e.g. https://bim.amentum.com/api/models/upload)
///
/// If the variable is not set, the upload is skipped and a "not configured" message
/// is returned — so the scan commands work offline without any server dependency.
///
/// This replaces the original Python localhost service. Authentication (Bearer token,
/// Windows Integrated, API key, mTLS) is left to the enterprise integration layer —
/// set AMENTUM_UPLOAD_BEARER_TOKEN to add a Bearer header automatically.
/// </summary>
public static class ModelUploadService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };

    /// <summary>Returns a null response with a skip reason if no endpoint is configured.</summary>
    public static async Task<(UploadModelResponse? Response, string? SkipReason)> UploadAsync(
        string filePath, CancellationToken ct)
    {
        string? url = Environment.GetEnvironmentVariable("AMENTUM_UPLOAD_URL");
        if (string.IsNullOrWhiteSpace(url))
            return (null, "AMENTUM_UPLOAD_URL is not set — upload skipped.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found for upload.", filePath);

        // Optional Bearer token
        string? token = Environment.GetEnvironmentVariable("AMENTUM_UPLOAD_BEARER_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

        using var form    = new MultipartFormDataContent();
        var       bytes   = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        var       content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", Path.GetFileName(filePath));

        using var resp = await _http.PostAsync(url, form, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var uploadResponse = await resp.Content
            .ReadFromJsonAsync<UploadModelResponse>(cancellationToken: ct)
            .ConfigureAwait(false);

        return (uploadResponse, null);
    }
}
