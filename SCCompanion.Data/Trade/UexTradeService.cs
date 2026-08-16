using System.Net.Http.Json;

namespace SCCompanion.Data.Trade;

/// <summary>
/// Loads the public UEX commodity/terminal price snapshot.
/// </summary>
public sealed class UexTradeService
{
    internal static readonly Uri PricesUri =
        new("https://api.uexcorp.uk/2.0/commodities_prices_all");

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyList<UexPriceEntry>? _cachedEntries;

    public UexTradeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<UexPriceEntry>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cachedEntries is not null)
        {
            return _cachedEntries;
        }

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedEntries is not null)
            {
                return _cachedEntries;
            }

            using HttpResponseMessage response = await _httpClient.GetAsync(
                PricesUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            UexResponse<List<UexPriceEntry>>? payload =
                await response.Content.ReadFromJsonAsync<UexResponse<List<UexPriceEntry>>>(
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (payload?.Data is null)
            {
                throw new InvalidDataException(
                    payload?.Message ?? "UEX returned no commodity price data.");
            }

            _cachedEntries = payload.Data
                .Where(entry => !string.IsNullOrWhiteSpace(entry.CommodityName) &&
                                !string.IsNullOrWhiteSpace(entry.TerminalName))
                .ToArray();
            return _cachedEntries;
        }
        finally
        {
            _loadGate.Release();
        }
    }
}
