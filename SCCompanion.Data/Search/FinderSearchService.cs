using System.Text.Json;
using System.Text.Json.Serialization;

namespace SCCompanion.Data.Search;

/// <summary>
/// Loads and caches the CStone item index, then performs local predictive searches.
/// </summary>
public sealed class FinderSearchService
{
    private static readonly Uri SearchIndexUri = new("https://finder.cstone.space/GetSearch");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<FinderItem>? _items;

    public FinderSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task PreloadAsync(CancellationToken cancellationToken = default)
    {
        _ = await GetItemsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinderItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Trim().Length < 2)
        {
            return [];
        }

        IReadOnlyList<FinderItem> items = await GetItemsAsync(cancellationToken);
        return FinderSearchEngine.Search(items, query);
    }

    private async Task<IReadOnlyList<FinderItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        if (_items is not null)
        {
            return _items;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_items is not null)
            {
                return _items;
            }

            using HttpResponseMessage response = await _httpClient.GetAsync(
                SearchIndexUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            List<FinderItemDto>? records = await JsonSerializer.DeserializeAsync<List<FinderItemDto>>(
                content,
                SerializerOptions,
                cancellationToken);

            _items = records?
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.Id) &&
                    !string.IsNullOrWhiteSpace(record.Name))
                .Select(record => new FinderItem(
                    record.Id.Trim(),
                    record.Name.Trim(),
                    record.Sold == 1))
                .ToArray() ?? [];

            return _items;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private sealed class FinderItemDto
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("Sold")]
        public int Sold { get; init; }
    }
}
