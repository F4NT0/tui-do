using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TuiDo.Installer;

internal sealed record Asset([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("browser_download_url")] string Url);

internal sealed record Release([property: JsonPropertyName("tag_name")] string Tag, [property: JsonPropertyName("assets")] List<Asset> Assets);

internal static class GitHub
{
    public static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("tuido-installer/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    public static async Task<Release> LatestAsync(string repository) =>
        await Http.GetFromJsonAsync<Release>($"https://api.github.com/repos/{repository}/releases/latest")
        ?? throw new InvalidOperationException($"No release found for {repository}.");

    public static async Task DownloadAsync(string url, string path, Action<double> progress)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = File.Create(path);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, n));
            read += n;
            if (total > 0) progress(read * 100.0 / total.Value);
        }
        progress(100);
    }
}
