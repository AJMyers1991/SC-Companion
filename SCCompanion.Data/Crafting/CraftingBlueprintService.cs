using System.Text.Json;

namespace SCCompanion.Data.Crafting;

/// <summary>
/// Searches and hydrates blueprint records from the SC Craft API.
/// </summary>
public sealed class CraftingBlueprintService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public CraftingBlueprintService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CraftingBlueprint>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return [];
        }

        var uri = new Uri(
            "https://sc-craft.tools/api/blueprints" +
            $"?search={Uri.EscapeDataString(normalizedQuery)}&page=1");
        using HttpResponseMessage response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
        CraftingBlueprintsResponse? payload =
            await JsonSerializer.DeserializeAsync<CraftingBlueprintsResponse>(
                content,
                SerializerOptions,
                cancellationToken);

        return payload?.Items?
            .Where(IsDisplayable)
            .ToArray() ?? [];
    }

    public async Task<CraftingBlueprint?> FindByIdAsync(
        long blueprintId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blueprintId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        IReadOnlyList<CraftingBlueprint> matches = await SearchAsync(
            displayName,
            cancellationToken);
        return matches.FirstOrDefault(item => item.Id == blueprintId);
    }

    private static bool IsDisplayable(CraftingBlueprint blueprint) =>
        blueprint.Id is > 0 && !string.IsNullOrWhiteSpace(blueprint.Name);
}
