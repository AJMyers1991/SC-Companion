using SCCompanion.Data.Guides;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class GuidePageNavigatorTests
{
    [TestMethod]
    public void Next_FromLastPage_WrapsToFirstPage()
    {
        Assert.AreEqual(0, GuidePageNavigator.Next(currentIndex: 4, pageCount: 5));
    }

    [TestMethod]
    public void Previous_FromFirstPage_WrapsToLastPage()
    {
        Assert.AreEqual(4, GuidePageNavigator.Previous(currentIndex: 0, pageCount: 5));
    }

    [TestMethod]
    public void Next_FromMiddlePage_AdvancesOnePage()
    {
        Assert.AreEqual(3, GuidePageNavigator.Next(currentIndex: 2, pageCount: 5));
    }

    [TestMethod]
    public void Previous_FromMiddlePage_MovesBackOnePage()
    {
        Assert.AreEqual(1, GuidePageNavigator.Previous(currentIndex: 2, pageCount: 5));
    }

    [TestMethod]
    public void SinglePageGuide_RemainsOnItsOnlyPage()
    {
        Assert.AreEqual(0, GuidePageNavigator.Next(currentIndex: 0, pageCount: 1));
        Assert.AreEqual(0, GuidePageNavigator.Previous(currentIndex: 0, pageCount: 1));
    }

    [TestMethod]
    public void Navigation_RejectsAnEmptyGuide()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => GuidePageNavigator.Next(currentIndex: 0, pageCount: 0));
    }
}
