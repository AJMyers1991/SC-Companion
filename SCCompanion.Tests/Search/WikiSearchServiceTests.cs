using System.Net;
using System.Text;
using SCCompanion.Data.Search;

namespace SCCompanion.Tests.Search;

[TestClass]
public sealed class WikiSearchServiceTests
{
    [TestMethod]
    public async Task SearchAsync_RequestsPrefixSearchAndMapsArticles()
    {
        var handler = new StubHttpMessageHandler(
            """
            {
              "query": {
                "prefixsearch": [
                  { "pageid": 415, "title": "Carrack" },
                  { "pageid": 37401, "title": "Carrack Expedition" }
                ]
              }
            }
            """);
        using var httpClient = new HttpClient(handler);
        var service = new WikiSearchService(httpClient);

        IReadOnlyList<WikiArticleSearchResult> results = await service.SearchAsync("Carrack");

        Assert.IsNotNull(handler.RequestUri);
        StringAssert.Contains(handler.RequestUri.AbsoluteUri, "action=query");
        StringAssert.Contains(handler.RequestUri.AbsoluteUri, "list=prefixsearch");
        StringAssert.Contains(handler.RequestUri.AbsoluteUri, "pssearch=Carrack");
        Assert.HasCount(2, results);
        Assert.AreEqual(415L, results[0].PageId);
        Assert.AreEqual("Carrack", results[0].Title);
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
