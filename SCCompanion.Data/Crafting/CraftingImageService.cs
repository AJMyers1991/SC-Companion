using System.Text.Json;

namespace SCCompanion.Data.Crafting;

/// <summary>
/// Resolves crafted-item thumbnails through the Star Citizen Tools MediaWiki API.
/// </summary>
public sealed class CraftingImageService
{
    private static readonly Uri ApiUri = new("https://starcitizen.tools/api.php");
    private readonly HttpClient _httpClient;

    public CraftingImageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> FindImageAsync(
        string itemName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        string normalizedName = itemName.Trim();
        var searchUri = new Uri(
            $"{ApiUri}?action=query&format=json&list=prefixsearch" +
            $"&pssearch={Uri.EscapeDataString(normalizedName)}&pslimit=3");
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
        (long PageId, string Title) candidate = candidates.FirstOrDefault(item =>
            string.Equals(item.Title, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (candidate.PageId <= 0)
        {
            candidate = candidates.FirstOrDefault();
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
                PageId: item.TryGetProperty("pageid", out JsonElement id) ? id.GetInt64() : 0,
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
}
