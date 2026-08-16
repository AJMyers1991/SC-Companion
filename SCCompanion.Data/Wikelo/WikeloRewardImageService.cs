using System.Text.Json;
using System.Text.RegularExpressions;

namespace SCCompanion.Data.Wikelo;

/// <summary>
/// Resolves Wikelo reward artwork through the Star Citizen Tools MediaWiki API.
/// </summary>
public sealed partial class WikeloRewardImageService
{
    private static readonly Uri ApiUri = new("https://starcitizen.tools/api.php");

    private readonly HttpClient _httpClient;

    public WikeloRewardImageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> FindRewardImageAsync(
        string rewardName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardName);
        string normalizedName = rewardName.Trim();

        string? exactImage = await FindImageForTermAsync(
            normalizedName,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(exactImage))
        {
            return exactImage;
        }

        string baseName = QuotedEditionRegex()
            .Replace(normalizedName, string.Empty)
            .Trim();
        baseName = MultipleWhitespaceRegex().Replace(baseName, " ");
        if (baseName.Length == 0 ||
            string.Equals(baseName, normalizedName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await FindImageForTermAsync(baseName, cancellationToken);
    }

    private async Task<string?> FindImageForTermAsync(
        string searchTerm,
        CancellationToken cancellationToken)
    {
        string encodedTerm = Uri.EscapeDataString(searchTerm);
        var searchUri = new Uri(
            $"{ApiUri}?action=query&format=json&list=prefixsearch" +
            $"&pssearch={encodedTerm}&pslimit=5");

        using HttpResponseMessage searchResponse = await _httpClient.GetAsync(
            searchUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        searchResponse.EnsureSuccessStatusCode();

        await using Stream searchContent = await searchResponse.Content
            .ReadAsStreamAsync(cancellationToken);
        using JsonDocument searchDocument = await JsonDocument.ParseAsync(
            searchContent,
            cancellationToken: cancellationToken);

        (long PageId, string Title)[] candidates = ParseCandidates(searchDocument);
        (long PageId, string Title) candidate = candidates
            .FirstOrDefault(item => string.Equals(
                item.Title,
                searchTerm,
                StringComparison.OrdinalIgnoreCase));
        if (candidate.PageId <= 0)
        {
            candidate = candidates.FirstOrDefault(item =>
                IsCloseMatch(searchTerm, item.Title));
        }

        if (candidate.PageId <= 0)
        {
            return null;
        }

        var detailUri = new Uri(
            $"{ApiUri}?action=query&format=json&prop=pageimages" +
            $"&pageids={candidate.PageId}&pithumbsize=500");
        using HttpResponseMessage detailResponse = await _httpClient.GetAsync(
            detailUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        detailResponse.EnsureSuccessStatusCode();

        await using Stream detailContent = await detailResponse.Content
            .ReadAsStreamAsync(cancellationToken);
        using JsonDocument detailDocument = await JsonDocument.ParseAsync(
            detailContent,
            cancellationToken: cancellationToken);

        return ParseThumbnail(detailDocument);
    }

    private static (long PageId, string Title)[] ParseCandidates(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("query", out JsonElement query) ||
            !query.TryGetProperty("prefixsearch", out JsonElement prefixSearch) ||
            prefixSearch.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return prefixSearch.EnumerateArray()
            .Select(item => (
                PageId: item.TryGetProperty("pageid", out JsonElement pageId)
                    ? pageId.GetInt64()
                    : 0,
                Title: item.TryGetProperty("title", out JsonElement title)
                    ? title.GetString()?.Trim() ?? string.Empty
                    : string.Empty))
            .Where(item => item.PageId > 0 && item.Title.Length > 0)
            .ToArray();
    }

    private static string? ParseThumbnail(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("query", out JsonElement query) ||
            !query.TryGetProperty("pages", out JsonElement pages) ||
            pages.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty page in pages.EnumerateObject())
        {
            if (page.Value.TryGetProperty("thumbnail", out JsonElement thumbnail) &&
                thumbnail.TryGetProperty("source", out JsonElement source))
            {
                return source.GetString();
            }
        }

        return null;
    }

    private static bool IsCloseMatch(string searchTerm, string title)
    {
        HashSet<string> titleWords = SplitSignificantWords(title).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        string[] searchWords = SplitSignificantWords(searchTerm);
        if (searchWords.Length == 0)
        {
            return true;
        }

        int matches = searchWords.Count(titleWords.Contains);
        return (double)matches / searchWords.Length >= 0.5d;
    }

    private static string[] SplitSignificantWords(string value)
    {
        return WordSeparatorRegex()
            .Split(value.Trim().ToLowerInvariant())
            .Where(word => word.Length > 2)
            .ToArray();
    }

    [GeneratedRegex("""['"][^'"]*['"]""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedEditionRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex("""[\s'"-]+""", RegexOptions.CultureInvariant)]
    private static partial Regex WordSeparatorRegex();
}
