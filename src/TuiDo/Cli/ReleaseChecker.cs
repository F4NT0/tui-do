using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TuiDo.Cli;

internal sealed record ReleaseInfo(string Tag, string Url);

internal interface IReleaseChecker
{
    /// <summary>Latest published release, or null when the repository has none. Throws when it cannot be reached.</summary>
    Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken = default);
}

internal sealed class GitHubReleaseChecker : IReleaseChecker
{
    private sealed record Dto([property: JsonPropertyName("tag_name")] string TagName, [property: JsonPropertyName("html_url")] string HtmlUrl);

    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Command}/{AppInfo.Version}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var response = await http.GetAsync($"https://api.github.com/repos/{AppInfo.Repository}/releases/latest", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<Dto>(cancellationToken);
        return dto is null ? null : new ReleaseInfo(dto.TagName, dto.HtmlUrl);
    }
}

internal static class VersionComparer
{
    public static bool IsDifferent(string remote, string local) => Parse(remote) != Parse(local);

    public static bool IsNewer(string remote, string local) => Parse(remote) > Parse(local);

    private static Version Parse(string v) =>
        Version.TryParse(v.TrimStart('v', 'V').Split('-', '+')[0], out var parsed) ? parsed : new Version(0, 0, 0);
}
