using System.Net.Http.Headers;
using System.Text.Json;

namespace OptiDiag.Infrastructure;

public sealed record ReleaseAssetInfo(string Name, Uri DownloadUrl, long Size, string? Digest);

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string Tag,
    string Title,
    Uri ReleasePage,
    bool IsUpdateAvailable,
    IReadOnlyList<ReleaseAssetInfo> Assets);

public sealed class GitHubReleaseUpdateService
{
    private readonly HttpClient _client;
    private readonly string _repository;

    public GitHubReleaseUpdateService(HttpClient? client = null, string repository = "cheguevara-great-man/OptiDiag")
    {
        _client = client ?? new HttpClient();
        _repository = repository;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
        {
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OptiDiag", "0.6"));
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(
            $"https://api.github.com/repos/{_repository}/releases/latest",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Parse(document.RootElement, currentVersion);
    }

    public static UpdateCheckResult Parse(JsonElement root, Version currentVersion)
    {
        var tag = RequiredString(root, "tag_name");
        var versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var latestVersion))
        {
            throw new InvalidDataException($"GitHub Release 标签不是可识别的版本号：{tag}");
        }

        var page = new Uri(RequiredString(root, "html_url"), UriKind.Absolute);
        var title = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? tag
            : tag;
        var assets = new List<ReleaseAssetInfo>();
        if (root.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetArray.EnumerateArray())
            {
                var assetName = RequiredString(asset, "name");
                var downloadUrl = new Uri(RequiredString(asset, "browser_download_url"), UriKind.Absolute);
                var size = asset.TryGetProperty("size", out var sizeNode) && sizeNode.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0;
                var digest = asset.TryGetProperty("digest", out var digestNode)
                    && digestNode.ValueKind == JsonValueKind.String
                        ? digestNode.GetString()
                        : null;
                assets.Add(new ReleaseAssetInfo(assetName, downloadUrl, size, digest));
            }
        }

        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            tag,
            title,
            page,
            latestVersion > currentVersion,
            assets);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"GitHub Release 响应缺少 {propertyName}。");
        }

        return property.GetString()!;
    }
}
