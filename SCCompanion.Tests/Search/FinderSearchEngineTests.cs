using SCCompanion.Data.Search;

namespace SCCompanion.Tests.Search;

[TestClass]
public sealed class FinderSearchEngineTests
{
    [TestMethod]
    public void Search_PutsPrefixMatchesBeforeOtherContainsMatches()
    {
        FinderItem[] items =
        [
            new("1", "Cutlass Black", true),
            new("2", "Black Paint", false),
            new("3", "Arrow", true),
            new("4", "Black Kite Helmet", true)
        ];

        IReadOnlyList<FinderItem> results = FinderSearchEngine.Search(items, "bLaCk");

        CollectionAssert.AreEqual(
            new[] { "Black Paint", "Black Kite Helmet", "Cutlass Black" },
            results.Select(item => item.Name).ToArray());
    }

    [TestMethod]
    public void Search_ReturnsEmptyBeforeTwoCharacters()
    {
        FinderItem[] items =
        [
            new("1", "Arrow", true),
            new("2", "Aurora", true)
        ];

        IReadOnlyList<FinderItem> results = FinderSearchEngine.Search(items, "A");

        Assert.IsEmpty(results);
    }
}
