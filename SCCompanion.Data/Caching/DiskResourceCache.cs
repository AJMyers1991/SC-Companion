using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;

namespace SCCompanion.Data.Caching;

/// <summary>
/// Lazily downloads remote resources and stores them in a persistent disk cache.
/// Concurrent requests for the same resource share one download.
/// </summary>
public sealed class DiskResourceCache
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheRoot;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _resourceLocks = new(StringComparer.Ordinal);

    public DiskResourceCache(HttpClient httpClient, string cacheRoot)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);

        _httpClient = httpClient;
        _cacheRoot = cacheRoot;
    }

    public async Task<string> GetOrDownloadAsync(
        Uri resourceUri,
        string category,
        string? cacheKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);
        string normalizedCategory = NormalizeCategory(category);
        string cacheDirectory = Path.Combine(_cacheRoot, normalizedCategory);
        string cachePath = Path.Combine(
            cacheDirectory,
            BuildFileName(resourceUri, cacheKey));

        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        SemaphoreSlim resourceLock = _resourceLocks.GetOrAdd(
            cachePath,
            static _ => new SemaphoreSlim(1, 1));
        await resourceLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            Directory.CreateDirectory(cacheDirectory);
            string temporaryPath = cachePath + ".download";

            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(
                    resourceUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await using (Stream source = await response.Content
                                 .ReadAsStreamAsync(cancellationToken))
                await using (var destination = new FileStream(
                                 temporaryPath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize: 81920,
                                 useAsync: true))
                {
                    await source.CopyToAsync(destination, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, cachePath, overwrite: true);
                return cachePath;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            resourceLock.Release();
        }
    }

    public string? TryGetCachedPath(string category, string cacheKey)
    {
        string cacheDirectory = Path.Combine(_cacheRoot, NormalizeCategory(category));
        if (!Directory.Exists(cacheDirectory))
        {
            return null;
        }

        string prefix = BuildHash(cacheKey);
        return Directory.EnumerateFiles(cacheDirectory, $"{prefix}.*")
            .FirstOrDefault();
    }

    private static string BuildFileName(Uri resourceUri, string? cacheKey)
    {
        string hash = BuildHash(cacheKey ?? resourceUri.AbsoluteUri);
        string extension = Path.GetExtension(resourceUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = ".bin";
        }

        return $"{hash}{extension.ToLowerInvariant()}";
    }

    private static string BuildHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string normalized = new(
            category.Trim()
                .Select(character => invalidCharacters.Contains(character) ? '_' : character)
                .ToArray());
        return normalized.Length == 0 ? "resources" : normalized;
    }
}
