using System.Net;
using System.Text;
using SCCompanion.Data.Search;

namespace SCCompanion.Tests.Search;

[TestClass]
public sealed class FinderSearchServiceTests
{
    [TestMethod]
    public async Task SearchAsync_LoadsCStoneIndexAndMapsAvailability()
    {
        var handler = new StubHttpMessageHandler(
            """
            [
              { "id": "item-1", "name": "Arrow", "Sold": 1 },
              { "id": "item-2", "name": "Arrowhead Paint", "Sold": 0 }
            ]
            """);
        using var httpClient = new HttpClient(handler);
        var service = new FinderSearchService(httpClient);

        IReadOnlyList<FinderItem> results = await service.SearchAsync("arr");

        Assert.AreEqual("https://finder.cstone.space/GetSearch", handler.RequestUri?.AbsoluteUri);
        Assert.HasCount(2, results);
        Assert.IsTrue(results[0].IsAvailable);
        Assert.IsFalse(results[1].IsAvailable);
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
