using System.Text.Json;
using System.Text.RegularExpressions;

namespace SCCompanion.Data.Wikelo;

/// <summary>
/// Downloads and parses the currently enabled Wikelo Trades catalog.
/// </summary>
public sealed partial class WikeloTradeService
{
    private static readonly Uri BaseUri = new("https://wikelotrades.com/");
    private const string FallbackTradeSource = "scripts/trades/PATCH_4_8_1.js";

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<WikeloTrade>? _cachedTrades;

    public WikeloTradeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WikeloTrade>> LoadTradesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cachedTrades is not null)
        {
            return _cachedTrades;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedTrades is not null)
            {
                return _cachedTrades;
            }

            string sourcePath = await ResolveLatestTradeSourceAsync(cancellationToken);
            string script = await _httpClient.GetStringAsync(
                new Uri(BaseUri, sourcePath),
                cancellationToken);

            _cachedTrades = ParseTradeScript(script);
            return _cachedTrades;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public static IReadOnlyList<WikeloTrade> ParseTradeScript(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        int start = script.IndexOf('[', StringComparison.Ordinal);
        int end = script.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        string arrayText = script[start..(end + 1)];
        string quotedKeys = JavaScriptKeyRegex().Replace(
            arrayText,
            match => $"\"{match.Groups[1].Value}\"");
        string json = TrailingCommaRegex().Replace(quotedKeys, string.Empty);

        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

        var trades = new List<WikeloTrade>();
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            string id = GetString(element, "id");
            string missionName = GetString(element, "missionName");
            if (id.Length == 0 || missionName.Length == 0)
            {
                continue;
            }

            trades.Add(new WikeloTrade(
                id,
                missionName,
                GetStringOrArray(element, "rewardName"),
                GetString(element, "category"),
                GetString(element, "patch"),
                GetString(element, "reputation"),
                ParseRequiredItems(element),
                GetString(element, "description"),
                GetBoolean(element, "active", defaultValue: true)));
        }

        return trades;
    }

    private async Task<string> ResolveLatestTradeSourceAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            string manifest = await _httpClient.GetStringAsync(
                new Uri(BaseUri, "scripts/trades/manifest.js"),
                cancellationToken);
            MatchCollection matches = EnabledPatchRegex().Matches(manifest);
            if (matches.Count > 0)
            {
                return matches[^1].Groups["src"].Value;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            // The known current patch remains available when the manifest cannot be loaded.
        }

        return FallbackTradeSource;
    }

    private static IReadOnlyList<WikeloRequiredItem> ParseRequiredItems(JsonElement trade)
    {
        if (!trade.TryGetProperty("requiredItems", out JsonElement requiredItems) ||
            requiredItems.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<WikeloRequiredItem>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int sourceOrder = 0;
        foreach (JsonElement item in requiredItems.EnumerateArray())
        {
            string name = GetString(item, "items");
            if (name.Length == 0)
            {
                continue;
            }

            int quantity = GetInt32(item, "quantity", 1);
            string baseId = BuildIngredientId(name);
            string id = baseId;
            int duplicate = 2;
            while (!usedIds.Add(id))
            {
                id = $"{baseId}-{duplicate++}";
            }

            parsed.Add(new WikeloRequiredItem(
                id,
                name,
                Math.Max(1, quantity),
                sourceOrder++));
        }

        return parsed;
    }

    private static string BuildIngredientId(string name)
    {
        string id = string.Join(
            '-',
            NonAlphaNumericRegex()
                .Split(name.Trim().ToLowerInvariant())
                .Where(part => part.Length > 0));
        return id.Length == 0 ? "item" : id;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static string GetStringOrArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Array => string.Join(
                ", ",
                property.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };
    }

    private static int GetInt32(
        JsonElement element,
        string propertyName,
        int defaultValue)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
               property.TryGetInt32(out int value)
            ? value
            : defaultValue;
    }

    private static bool GetBoolean(
        JsonElement element,
        string propertyName,
        bool defaultValue)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
               (property.ValueKind == JsonValueKind.True ||
                property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : defaultValue;
    }

    [GeneratedRegex(@"(?<=[\s,\[\{])(\w+)(?=\s*:)", RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptKeyRegex();

    [GeneratedRegex(@",\s*(?=[}\]])", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingCommaRegex();

    [GeneratedRegex("""\{\s*patch:\s*"(?<patch>[^"]+)"\s*,\s*src:\s*"(?<src>[^"]+)"\s*,\s*enabled:\s*true\s*\}""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EnabledPatchRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();
}
