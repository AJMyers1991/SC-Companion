namespace SCCompanion.Data.Search;

/// <summary>
/// Centralizes visibility rules shared by predictive-search pages.
/// </summary>
public static class SearchPresentationPolicy
{
    public static bool ShouldShowRecentSearches(
        bool isFocused,
        string? query,
        int recentCount)
    {
        return isFocused &&
               string.IsNullOrWhiteSpace(query) &&
               recentCount > 0;
    }
}
