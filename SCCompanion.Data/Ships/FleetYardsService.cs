using System.Text.Json;

namespace SCCompanion.Data.Ships;

public sealed class FleetYardsService
{
    private static readonly Uri ApiRoot = new("https://api.fleetyards.net/v1/");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public FleetYardsService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<FleetYardsShip>> GetAllShipsAsync(
        CancellationToken cancellationToken = default)
    {
        FleetYardsModelsResponse first = await GetAsync<FleetYardsModelsResponse>(
            "models?page=1&perPage=240", cancellationToken);
        var ships = first.Items ?? [];
        int totalPages = Math.Max(1, first.Meta?.Pagination?.TotalPages ?? 1);
        for (int page = 2; page <= totalPages; page++)
        {
            try
            {
                FleetYardsModelsResponse next = await GetAsync<FleetYardsModelsResponse>(
                    $"models?page={page}&perPage=240", cancellationToken);
                if (next.Items is { Count: > 0 }) ships.AddRange(next.Items);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Preserve Kotlin behavior: a failed later page does not discard successful pages.
            }
        }
        return ships;
    }

    public async Task<ShipDetailData> GetDetailDataAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Task<IReadOnlyList<FleetYardsHardpoint>> hardpoints = GetListOrEmptyAsync<FleetYardsHardpoint>($"models/{Uri.EscapeDataString(slug)}/hardpoints", cancellationToken);
        Task<IReadOnlyList<FleetYardsModule>> modules = GetListOrEmptyAsync<FleetYardsModule>($"models/{Uri.EscapeDataString(slug)}/modules", cancellationToken);
        Task<IReadOnlyList<FleetYardsPaint>> paints = GetListOrEmptyAsync<FleetYardsPaint>($"models/{Uri.EscapeDataString(slug)}/paints", cancellationToken);
        await Task.WhenAll(hardpoints, modules, paints);
        return new ShipDetailData(await hardpoints, await modules, await paints);
    }

    private async Task<IReadOnlyList<T>> GetListOrEmptyAsync<T>(string relativeUri, CancellationToken cancellationToken)
    {
        try { return await GetAsync<List<T>>(relativeUri, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    private async Task<T> GetAsync<T>(string relativeUri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(new Uri(ApiRoot, relativeUri), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
               ?? throw new JsonException("FleetYards returned an empty response.");
    }
}
