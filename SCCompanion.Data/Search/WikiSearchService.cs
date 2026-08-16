using System.Text.Json;
using System.Text.Json.Serialization;

namespace SCCompanion.Data.Search;

/// <summary>
/// Represents one Star Citizen Tools predictive article result.
/// </summary>
public sealed record WikiArticleSearchResult(long PageId, string Title, string Snippet);

/// <summary>
/// Searches the Star Citizen Tools MediaWiki API by article-title prefix.
/// </summary>
public sealed class WikiSearchService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public WikiSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WikiArticleSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return [];
        }

        string encodedQuery = Uri.EscapeDataString(normalizedQuery);
        var requestUri = new Uri(
            "https://starcitizen.tools/api.php" +
            "?action=query&format=json&list=prefixsearch" +
            $"&pssearch={encodedQuery}&pslimit=20");

        using HttpResponseMessage response = await _httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        WikiSearchResponse? payload = await JsonSerializer.DeserializeAsync<WikiSearchResponse>(
            content,
            SerializerOptions,
            cancellationToken);

        return payload?.Query?.PrefixSearch?
            .Where(result =>
                result.PageId > 0 &&
                !string.IsNullOrWhiteSpace(result.Title))
            .Select(result => new WikiArticleSearchResult(
                result.PageId,
                result.Title.Trim(),
                string.Empty))
            .ToArray() ?? [];
    }

    private sealed class WikiSearchResponse
    {
        public WikiSearchQuery? Query { get; init; }
    }

    private sealed class WikiSearchQuery
    {
        [JsonPropertyName("prefixsearch")]
        public List<WikiSearchResult>? PrefixSearch { get; init; }
    }

    private sealed class WikiSearchResult
    {
        [JsonPropertyName("pageid")]
        public long PageId { get; init; }

        public string Title { get; init; } = string.Empty;
    }
}
