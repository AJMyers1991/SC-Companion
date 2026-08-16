using System.Net;
using SCCompanion.Data.Caching;

namespace SCCompanion.Tests.Caching;

[TestClass]
public sealed class DiskResourceCacheTests
{
    [TestMethod]
    public async Task GetOrDownloadAsync_DownloadsOnceAndReusesPersistentFile()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), $"sccompanion-cache-{Guid.NewGuid():N}");
        var handler = new CountingHandler([1, 2, 3, 4]);
        using var client = new HttpClient(handler);
        var cache = new DiskResourceCache(client, cacheRoot);
        var resourceUri = new Uri("https://example.test/guide.png");

        try
        {
            string firstPath = await cache.GetOrDownloadAsync(resourceUri, "guides", "guide-page-1");
            string secondPath = await cache.GetOrDownloadAsync(resourceUri, "guides", "guide-page-1");

            Assert.AreEqual(firstPath, secondPath);
            Assert.AreEqual(firstPath, cache.TryGetCachedPath("guides", "guide-page-1"));
            Assert.AreEqual(1, handler.RequestCount);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(firstPath));
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetOrDownloadAsync_AllowsDifferentResourcesToDownloadConcurrently()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), $"sccompanion-cache-{Guid.NewGuid():N}");
        var handler = new ConcurrentRequestHandler();
        using var client = new HttpClient(handler);
        var cache = new DiskResourceCache(client, cacheRoot);

        try
        {
            Task<string> first = cache.GetOrDownloadAsync(new Uri("https://example.test/ship-a.png"), "ships", "ship-a");
            Task<string> second = cache.GetOrDownloadAsync(new Uri("https://example.test/ship-b.png"), "ships", "ship-b");

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.IsGreaterThanOrEqualTo(2, handler.MaximumConcurrentRequests);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetOrDownloadAsync_ConcurrentSameResourceDownloadsOnlyOnce()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), $"sccompanion-cache-{Guid.NewGuid():N}");
        var handler = new ConcurrentRequestHandler();
        using var client = new HttpClient(handler);
        var cache = new DiskResourceCache(client, cacheRoot);
        var uri = new Uri("https://example.test/shared.png");

        try
        {
            Task<string>[] requests = Enumerable.Range(0, 20)
                .Select(_ => cache.GetOrDownloadAsync(uri, "ships", "shared"))
                .ToArray();
            string[] paths = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(3));

            Assert.AreEqual(1, paths.Distinct(StringComparer.Ordinal).Count());
            Assert.AreEqual(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, recursive: true);
        }
    }

    private sealed class CountingHandler(byte[] responseBytes) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes)
            });
        }
    }

    private sealed class ConcurrentRequestHandler : HttpMessageHandler
    {
        private int _activeRequests;
        private int _maximumConcurrentRequests;
        private int _requestCount;

        public int MaximumConcurrentRequests => _maximumConcurrentRequests;
        public int RequestCount => _requestCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            int active = Interlocked.Increment(ref _activeRequests);
            int observed;
            do
            {
                observed = _maximumConcurrentRequests;
                if (active <= observed) break;
            }
            while (Interlocked.CompareExchange(ref _maximumConcurrentRequests, active, observed) != observed);

            try
            {
                await Task.Delay(100, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }
    }
}
