namespace SCCompanion.Models;

public sealed record GuideDefinition(
    string Name,
    IReadOnlyList<string> PagePaths,
    string? Attribution = null,
    string? AttributionUrl = null)
{
    public string PageDescription => PagePaths.Count == 1
        ? "1 page"
        : $"{PagePaths.Count} pages";
}
