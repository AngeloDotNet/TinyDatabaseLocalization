using System.Globalization;
using Microsoft.Data.Sqlite;
using TinyLocalization.Entities;
using TinyLocalization.Services;
using TinyLocalization.Services.Interfaces;

namespace TinyLocalization.IntegrationTests.Infrastructure;

internal static class TestData
{
    /// <summary>
    /// Creates a translation row. An empty culture is the invariant culture.
    /// </summary>
    public static Translation Tr(string resource, string key, string culture, string value)
        => new() { Resource = resource, Key = key, Culture = culture, Value = value };

    /// <summary>
    /// Deletes a temporary SQLite database file (and its journal files) after releasing the pooled connections.
    /// </summary>
    public static void DeleteDatabase(string path)
    {
        // pooled connections keep the file locked, which matters on Windows
        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // a leftover temporary file is not worth failing a test
            }
        }
    }
}

/// <summary>
/// Sets <see cref="CultureInfo.CurrentUICulture"/> for the duration of a block and restores it afterwards.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentUICulture;

    public CultureScope(string cultureName)
        => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

    public void Dispose() => CultureInfo.CurrentUICulture = previous;
}

internal sealed record PublishedSingle(string Resource, string Key, string Culture);

internal sealed record PublishedResource(string Resource, IReadOnlyList<InvalidationKey> Keys);

/// <summary>
/// An <see cref="ICacheInvalidationPublisher"/> that records what the manager publishes and can be told to fail.
/// </summary>
internal sealed class RecordingPublisher : ICacheInvalidationPublisher
{
    public List<PublishedSingle> Singles { get; } = [];

    public List<PublishedResource> Resources { get; } = [];

    public bool ThrowOnPublish { get; set; }

    public Task PublishSingleInvalidationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("The backplane is not reachable.");
        }

        Singles.Add(new PublishedSingle(resource, key, culture));

        return Task.CompletedTask;
    }

    public Task PublishResourceInvalidationAsync(string resource, IEnumerable<InvalidationKey> keys, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("The backplane is not reachable.");
        }

        Resources.Add(new PublishedResource(resource, keys.ToList()));

        return Task.CompletedTask;
    }
}
