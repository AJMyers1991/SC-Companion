namespace SCCompanion.Data.Search;

/// <summary>
/// Builds the external result destinations used by the in-app browser.
/// </summary>
public static class SearchResultUriBuilder
{
    public static Uri BuildFinderItemUri(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        string encodedId = Uri.EscapeDataString(itemId.Trim());
        return new Uri($"https://finder.cstone.space/Search/{encodedId}");
    }

    public static Uri BuildWikiArticleUri(long pageId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageId, 1);
        return new Uri(
            $"https://starcitizen.tools/?curid={pageId}&mobileaction=toggle_view_mobile");
    }
}
