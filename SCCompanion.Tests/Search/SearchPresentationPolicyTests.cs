using SCCompanion.Data.Search;

namespace SCCompanion.Tests.Search;

[TestClass]
public sealed class SearchPresentationPolicyTests
{
    [TestMethod]
    [DataRow(true, "", 3, true)]
    [DataRow(true, "Carrack", 3, false)]
    [DataRow(false, "", 3, false)]
    [DataRow(true, "", 0, false)]
    public void ShouldShowRecentSearches_RequiresFocusEmptyQueryAndStoredItems(
        bool isFocused,
        string query,
        int recentCount,
        bool expected)
    {
        bool result = SearchPresentationPolicy.ShouldShowRecentSearches(
            isFocused,
            query,
            recentCount);

        Assert.AreEqual(expected, result);
    }
}
