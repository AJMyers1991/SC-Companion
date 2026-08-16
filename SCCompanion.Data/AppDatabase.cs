using SCCompanion.Data.Entities;
using SQLite;

namespace SCCompanion.Data;

/// <summary>
/// Owns the application's single local SQLite connection and schema lifecycle.
/// </summary>
public sealed class AppDatabase : IAsyncDisposable
{
    public const string DatabaseFilename = "sccompanion.db3";

    private const int CurrentSchemaVersion = 5;
    private const int MaxRecentSearchesPerFeature = 10;

    private readonly string _databasePath;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _shipStateLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    /// <summary>
    /// Creates a database at the supplied platform-specific path. The special
    /// SQLite path ":memory:" can be used by automated tests.
    /// </summary>
    public AppDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public async Task InitializeAsync()
    {
        _ = await GetConnectionAsync();
    }

    public async Task<IReadOnlyList<FavoriteRecord>> GetFavoritesAsync(string category)
    {
        string normalizedCategory = NormalizeRequired(category, nameof(category));
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        return await connection.Table<FavoriteRecord>()
            .Where(record => record.Category == normalizedCategory)
            .OrderBy(record => record.DisplayName)
            .ToListAsync();
    }

    public async Task SaveFavoriteAsync(FavoriteRecord favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);

        favorite.Category = NormalizeRequired(favorite.Category, nameof(favorite.Category));
        favorite.ExternalId = NormalizeRequired(favorite.ExternalId, nameof(favorite.ExternalId));
        favorite.DisplayName = NormalizeRequired(favorite.DisplayName, nameof(favorite.DisplayName));
        favorite.CreatedUtc = favorite.CreatedUtc == default
            ? DateTime.UtcNow
            : favorite.CreatedUtc.ToUniversalTime();

        SQLiteAsyncConnection connection = await GetConnectionAsync();
        FavoriteRecord? existing = await connection.Table<FavoriteRecord>()
            .Where(record =>
                record.Category == favorite.Category &&
                record.ExternalId == favorite.ExternalId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            await connection.InsertAsync(favorite);
            return;
        }

        favorite.Id = existing.Id;
        favorite.CreatedUtc = existing.CreatedUtc;
        await connection.UpdateAsync(favorite);
    }

    public async Task<bool> RemoveFavoriteAsync(string category, string externalId)
    {
        string normalizedCategory = NormalizeRequired(category, nameof(category));
        string normalizedExternalId = NormalizeRequired(externalId, nameof(externalId));
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        int deletedRows = await connection.ExecuteAsync(
            "DELETE FROM Favorites WHERE Category = ? AND ExternalId = ?;",
            normalizedCategory,
            normalizedExternalId);

        return deletedRows > 0;
    }

    public async Task<bool> ToggleFavoriteAsync(
        string category,
        string externalId,
        string displayName)
    {
        string normalizedCategory = NormalizeRequired(category, nameof(category));
        string normalizedExternalId = NormalizeRequired(externalId, nameof(externalId));
        string normalizedDisplayName = NormalizeRequired(displayName, nameof(displayName));
        await _shipStateLock.WaitAsync();
        try
        {
            SQLiteAsyncConnection connection = await GetConnectionAsync();
            FavoriteRecord? existing = await connection.Table<FavoriteRecord>()
                .Where(record => record.Category == normalizedCategory && record.ExternalId == normalizedExternalId)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                await connection.DeleteAsync(existing);
                return false;
            }

            await connection.InsertAsync(new FavoriteRecord
            {
                Category = normalizedCategory,
                ExternalId = normalizedExternalId,
                DisplayName = normalizedDisplayName,
                CreatedUtc = DateTime.UtcNow
            });

            return true;
        }
        finally { _shipStateLock.Release(); }
    }

    public async Task<IReadOnlySet<string>> GetShipFleetIdsAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        List<ShipFleetRecord> records = await connection.Table<ShipFleetRecord>().ToListAsync();
        return records.Select(record => record.ShipId).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> IsShipInFleetAsync(string shipId)
    {
        string normalizedShipId = NormalizeRequired(shipId, nameof(shipId));
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<ShipFleetRecord>()
            .Where(record => record.ShipId == normalizedShipId)
            .CountAsync() > 0;
    }

    public async Task SetShipFleetMembershipAsync(
        string shipId,
        string displayName,
        bool isInFleet)
    {
        string normalizedShipId = NormalizeRequired(shipId, nameof(shipId));
        string normalizedDisplayName = NormalizeRequired(displayName, nameof(displayName));
        await _shipStateLock.WaitAsync();
        try
        {
            SQLiteAsyncConnection connection = await GetConnectionAsync();
            ShipFleetRecord? existing = await connection.Table<ShipFleetRecord>()
                .Where(record => record.ShipId == normalizedShipId)
                .FirstOrDefaultAsync();

            if (!isInFleet)
            {
                if (existing is not null) await connection.DeleteAsync(existing);
                return;
            }

            if (existing is null)
            {
                await connection.InsertAsync(new ShipFleetRecord
                {
                    ShipId = normalizedShipId,
                    DisplayName = normalizedDisplayName,
                    AddedUtc = DateTime.UtcNow
                });
            }
            else if (!string.Equals(existing.DisplayName, normalizedDisplayName, StringComparison.Ordinal))
            {
                existing.DisplayName = normalizedDisplayName;
                await connection.UpdateAsync(existing);
            }
        }
        finally { _shipStateLock.Release(); }
    }

    public async Task<string?> GetSelectionAsync(string feature, string selectionKey)
    {
        string normalizedFeature = NormalizeRequired(feature, nameof(feature));
        string normalizedSelectionKey = NormalizeRequired(selectionKey, nameof(selectionKey));
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        UserSelectionRecord? selection = await connection.Table<UserSelectionRecord>()
            .Where(record =>
                record.Feature == normalizedFeature &&
                record.SelectionKey == normalizedSelectionKey)
            .FirstOrDefaultAsync();

        return selection?.SelectedValue;
    }

    public async Task SetSelectionAsync(string feature, string selectionKey, string selectedValue)
    {
        var selection = new UserSelectionRecord
        {
            Feature = NormalizeRequired(feature, nameof(feature)),
            SelectionKey = NormalizeRequired(selectionKey, nameof(selectionKey)),
            SelectedValue = NormalizeRequired(selectedValue, nameof(selectedValue)),
            UpdatedUtc = DateTime.UtcNow
        };

        SQLiteAsyncConnection connection = await GetConnectionAsync();
        UserSelectionRecord? existing = await connection.Table<UserSelectionRecord>()
            .Where(record =>
                record.Feature == selection.Feature &&
                record.SelectionKey == selection.SelectionKey)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            await connection.InsertAsync(selection);
            return;
        }

        selection.Id = existing.Id;
        await connection.UpdateAsync(selection);
    }

    public async Task<bool> RemoveSelectionAsync(string feature, string selectionKey)
    {
        string normalizedFeature = NormalizeRequired(feature, nameof(feature));
        string normalizedSelectionKey = NormalizeRequired(selectionKey, nameof(selectionKey));
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        int deletedRows = await connection.ExecuteAsync(
            "DELETE FROM UserSelections WHERE Feature = ? AND SelectionKey = ?;",
            normalizedFeature,
            normalizedSelectionKey);

        return deletedRows > 0;
    }

    public async Task<IReadOnlyList<WikeloTradeProgressRecord>> GetWikeloTradeProgressAsync(
        string tradeId)
    {
        string normalizedTradeId = NormalizeRequired(tradeId, nameof(tradeId));
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        return await connection.Table<WikeloTradeProgressRecord>()
            .Where(record => record.TradeId == normalizedTradeId)
            .OrderBy(record => record.IngredientId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WikeloTradeProgressRecord>> GetAllWikeloTradeProgressAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<WikeloTradeProgressRecord>().ToListAsync();
    }

    public async Task SetWikeloTradeProgressAsync(
        string tradeId,
        string ingredientId,
        int ownedQuantity)
    {
        string normalizedTradeId = NormalizeRequired(tradeId, nameof(tradeId));
        string normalizedIngredientId = NormalizeRequired(ingredientId, nameof(ingredientId));
        ArgumentOutOfRangeException.ThrowIfNegative(ownedQuantity);
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        WikeloTradeProgressRecord? existing = await connection
            .Table<WikeloTradeProgressRecord>()
            .Where(record =>
                record.TradeId == normalizedTradeId &&
                record.IngredientId == normalizedIngredientId)
            .FirstOrDefaultAsync();

        if (ownedQuantity == 0)
        {
            if (existing is not null)
            {
                await connection.DeleteAsync(existing);
            }

            return;
        }

        var progress = new WikeloTradeProgressRecord
        {
            Id = existing?.Id ?? 0,
            TradeId = normalizedTradeId,
            IngredientId = normalizedIngredientId,
            OwnedQuantity = ownedQuantity,
            UpdatedUtc = DateTime.UtcNow
        };

        if (existing is null)
        {
            await connection.InsertAsync(progress);
        }
        else
        {
            await connection.UpdateAsync(progress);
        }
    }

    public async Task DeleteWikeloTradeProgressAsync(string tradeId)
    {
        string normalizedTradeId = NormalizeRequired(tradeId, nameof(tradeId));
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM WikeloTradeProgress WHERE TradeId = ?;",
            normalizedTradeId);
    }

    public async Task SaveCraftingBlueprintSummaryAsync(
        CraftingBlueprintSummaryRecord summary,
        bool markOpened)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(summary.BlueprintId);
        summary.DisplayName = NormalizeRequired(summary.DisplayName, nameof(summary.DisplayName));
        summary.Category = summary.Category?.Trim() ?? string.Empty;

        SQLiteAsyncConnection connection = await GetConnectionAsync();
        CraftingBlueprintSummaryRecord? existing = await connection
            .Table<CraftingBlueprintSummaryRecord>()
            .Where(record => record.BlueprintId == summary.BlueprintId)
            .FirstOrDefaultAsync();
        if (markOpened)
        {
            DateTime openedUtc = DateTime.UtcNow;
            CraftingBlueprintSummaryRecord? latest = await connection
                .Table<CraftingBlueprintSummaryRecord>()
                .OrderByDescending(record => record.LastOpenedUtc)
                .FirstOrDefaultAsync();
            if (latest?.LastOpenedUtc is DateTime latestUtc && openedUtc <= latestUtc)
            {
                openedUtc = latestUtc.AddTicks(1);
            }

            summary.LastOpenedUtc = openedUtc;
        }
        else if (existing is not null)
        {
            summary.LastOpenedUtc = existing.LastOpenedUtc;
        }

        if (existing is null)
        {
            await connection.InsertAsync(summary);
        }
        else
        {
            await connection.UpdateAsync(summary);
        }
    }

    public async Task<IReadOnlyList<CraftingBlueprintSummaryRecord>> GetRecentCraftingBlueprintsAsync(
        int limit = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<CraftingBlueprintSummaryRecord>()
            .Where(record => record.LastOpenedUtc != null)
            .OrderByDescending(record => record.LastOpenedUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<CraftingBlueprintSummaryRecord?> GetCraftingBlueprintSummaryAsync(
        long blueprintId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blueprintId);
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<CraftingBlueprintSummaryRecord>()
            .Where(record => record.BlueprintId == blueprintId)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<string>> GetRecentSearchesAsync(
        string feature,
        int limit = 10)
    {
        string normalizedFeature = NormalizeRequired(feature, nameof(feature));
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        List<RecentSearchRecord> records = await connection.Table<RecentSearchRecord>()
            .Where(record => record.Feature == normalizedFeature)
            .OrderByDescending(record => record.LastUsedUtc)
            .Take(limit)
            .ToListAsync();

        return records.Select(record => record.Query).ToArray();
    }

    public async Task AddRecentSearchAsync(string feature, string query)
    {
        string normalizedFeature = NormalizeRequired(feature, nameof(feature));
        string normalizedQuery = NormalizeRequired(query, nameof(query));
        string comparisonQuery = normalizedQuery.ToUpperInvariant();
        SQLiteAsyncConnection connection = await GetConnectionAsync();

        RecentSearchRecord? latest = await connection.Table<RecentSearchRecord>()
            .Where(record => record.Feature == normalizedFeature)
            .OrderByDescending(record => record.LastUsedUtc)
            .FirstOrDefaultAsync();
        DateTime lastUsedUtc = DateTime.UtcNow;
        if (latest is not null && lastUsedUtc <= latest.LastUsedUtc)
        {
            lastUsedUtc = latest.LastUsedUtc.AddTicks(1);
        }

        RecentSearchRecord? existing = await connection.Table<RecentSearchRecord>()
            .Where(record =>
                record.Feature == normalizedFeature &&
                record.NormalizedQuery == comparisonQuery)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            await connection.InsertAsync(new RecentSearchRecord
            {
                Feature = normalizedFeature,
                NormalizedQuery = comparisonQuery,
                Query = normalizedQuery,
                LastUsedUtc = lastUsedUtc
            });
        }
        else
        {
            existing.Query = normalizedQuery;
            existing.LastUsedUtc = lastUsedUtc;
            await connection.UpdateAsync(existing);
        }

        await connection.ExecuteAsync(
            """
            DELETE FROM RecentSearches
            WHERE Feature = ?
              AND Id NOT IN (
                  SELECT Id
                  FROM RecentSearches
                  WHERE Feature = ?
                  ORDER BY LastUsedUtc DESC, Id DESC
                  LIMIT ?
              );
            """,
            normalizedFeature,
            normalizedFeature,
            MaxRecentSearchesPerFeature);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection = null;
        }

        _initializationLock.Dispose();
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                _connection = new SQLiteAsyncConnection(
                    _databasePath,
                    SQLiteOpenFlags.ReadWrite |
                    SQLiteOpenFlags.Create |
                    SQLiteOpenFlags.SharedCache);

                await ApplyMigrationsAsync(_connection);
            }

            return _connection;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static async Task ApplyMigrationsAsync(SQLiteAsyncConnection connection)
    {
        int schemaVersion = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;");
        if (schemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {schemaVersion} is newer than supported version {CurrentSchemaVersion}.");
        }

        if (schemaVersion < 1)
        {
            await connection.CreateTableAsync<FavoriteRecord>();
            await connection.CreateTableAsync<UserSelectionRecord>();
            await connection.ExecuteAsync("PRAGMA user_version = 1;");
        }

        if (schemaVersion < 2)
        {
            await connection.CreateTableAsync<RecentSearchRecord>();
            await connection.ExecuteAsync("PRAGMA user_version = 2;");
        }

        if (schemaVersion < 3)
        {
            await connection.CreateTableAsync<WikeloTradeProgressRecord>();
            await connection.ExecuteAsync("PRAGMA user_version = 3;");
        }

        if (schemaVersion < 4)
        {
            await connection.CreateTableAsync<CraftingBlueprintSummaryRecord>();
            await connection.ExecuteAsync("PRAGMA user_version = 4;");
        }

        if (schemaVersion < 5)
        {
            await connection.CreateTableAsync<ShipFleetRecord>();
            await connection.ExecuteAsync("PRAGMA user_version = 5;");
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
