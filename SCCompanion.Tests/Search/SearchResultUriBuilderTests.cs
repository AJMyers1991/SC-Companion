using SCCompanion.Data.Search;

namespace SCCompanion.Tests.Search;

[TestClass]
public sealed class SearchResultUriBuilderTests
{
    [TestMethod]
    public void BuildFinderItemUri_EscapesItemIdentifier()
    {
        Uri uri = SearchResultUriBuilder.BuildFinderItemUri("item id/42");

        Assert.AreEqual(
            "https://finder.cstone.space/Search/item%20id%2F42",
            uri.AbsoluteUri);
    }

    [TestMethod]
    public void BuildWikiArticleUri_UsesPageIdAndMobileView()
    {
        Uri uri = SearchResultUriBuilder.BuildWikiArticleUri(415);

        Assert.AreEqual(
            "https://starcitizen.tools/?curid=415&mobileaction=toggle_view_mobile",
            uri.AbsoluteUri);
    }
}
