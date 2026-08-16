namespace SCCompanion.Data.Guides;

/// <summary>
/// Provides validated wrap-around navigation for multi-page guide sets.
/// </summary>
public static class GuidePageNavigator
{
    public static int Next(int currentIndex, int pageCount)
    {
        Validate(currentIndex, pageCount);
        return (currentIndex + 1) % pageCount;
    }

    public static int Previous(int currentIndex, int pageCount)
    {
        Validate(currentIndex, pageCount);
        return (currentIndex - 1 + pageCount) % pageCount;
    }

    private static void Validate(int currentIndex, int pageCount)
    {
        if (pageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageCount),
                pageCount,
                "A guide must contain at least one page.");
        }

        if (currentIndex < 0 || currentIndex >= pageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentIndex),
                currentIndex,
                "The current page index must identify an existing guide page.");
        }
    }
}
