using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Localization;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Services;

/// <summary>
/// Provides methods for managing localization resources, including adding, updating, removing, and invalidating
/// translations in both the database and cache.
/// </summary>
/// <remarks>The LocalizationManager ensures that changes to translations are reflected in both the underlying
/// database and the distributed cache. The localizer caches all the translations of a resource for a culture as a
/// single snapshot, so when a translation is added, updated, or removed the snapshot of its (resource, culture) is
/// invalidated locally and, if a publisher is provided, invalidation messages are sent to other instances to
/// maintain consistency across distributed environments.</remarks>
/// <param name="dbContext">The database context used to access and persist localization data.</param>
/// <param name="fusionCache">The cache instance used to store and retrieve localization resources for improved performance.</param>
/// <param name="options">The configuration options that control localization manager behavior, such as cache key prefix and other settings.</param>
/// <param name="logger">The logger used to record informational and warning messages related to localization operations.</param>
/// <param name="publisher">An optional publisher for broadcasting cache invalidation messages to other application instances.</param>
public class LocalizationManager(LocalizationDbContext dbContext, IFusionCache fusionCache, DbLocalizationOptions options, ILogger<LocalizationManager> logger,
    ICacheInvalidationPublisher? publisher = null) : ILocalizationManager
{
    private Task<Translation?> FindAsync(string resource, string key, string culture, CancellationToken cancellationToken)
        // The entity is modified or removed afterwards, so tracking must be explicit: the context may be
        // configured with NoTracking by default.
        => dbContext.Translations.AsTracking().FirstOrDefaultAsync(t
            => t.Resource == resource && t.Key == key && t.Culture == culture, cancellationToken);

    private async Task RemoveSnapshotFromCacheAsync(string resource, string culture)
    {
        var cacheKey = LocalizationCacheKeys.ForSnapshot(options.CacheKeyPrefix, resource, culture);

        // No cancellation token on purpose: the database change is already committed at this point and skipping
        // the invalidation would leave a stale snapshot in the cache.
        await fusionCache.RemoveAsync(cacheKey);

        logger.LogDebug("Removed local cache key {CacheKey}", cacheKey);
    }

    private async Task InvalidateSingleAsync(string resource, string key, string culture, CancellationToken cancellationToken)
    {
        // Invalidate the snapshot that contains this translation (it holds all the keys of the resource for the culture)
        await RemoveSnapshotFromCacheAsync(resource, culture);

        // Publish invalidation to other instances (if publisher is available)
        if (publisher != null)
        {
            try
            {
                await publisher.PublishSingleInvalidationAsync(resource, key, culture, cancellationToken);

                logger.LogInformation("Published single invalidation for {Resource}/{Key}/{Culture}", resource, key, culture);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish single invalidation for {Resource}/{Key}/{Culture}", resource, key, culture);
            }
        }
    }

    public async Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var existing = await FindAsync(translation.Resource, translation.Key, translation.Culture, cancellationToken);

        if (existing == null)
        {
            dbContext.Translations.Add(translation);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Added translation {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
            }
            catch (DbUpdateException)
            {
                // Another writer may have inserted the same (resource, key, culture) after our lookup and hit the
                // unique index: detach our entity and apply the value as an update instead.
                dbContext.Entry(translation).State = EntityState.Detached;

                existing = await FindAsync(translation.Resource, translation.Key, translation.Culture, cancellationToken);

                if (existing == null)
                {
                    // The failure had a different cause
                    throw;
                }

                existing.Value = translation.Value;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Updated translation {Resource}/{Key}/{Culture} after a concurrent insert", translation.Resource, translation.Key, translation.Culture);
            }
        }
        else
        {
            existing.Value = translation.Value;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Updated translation {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
        }

        await InvalidateSingleAsync(translation.Resource, translation.Key, translation.Culture, cancellationToken);
    }

    public async Task<bool> RemoveAsync(string resource, string key, string culture, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(resource, key, culture, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        dbContext.Translations.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Removed translation {Resource}/{Key}/{Culture}", resource, key, culture);

        await InvalidateSingleAsync(resource, key, culture, cancellationToken);

        return true;
    }

    public async Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        // Enumerate the known keys+cultures for this resource: the cultures tell which snapshots to drop locally,
        // the full list is what the publisher sends to the other instances.
        var entries = await dbContext.Translations
            .AsNoTracking()
            .Where(t => t.Resource == resource)
            .Select(t => new { t.Key, t.Culture })
            .ToListAsync(cancellationToken);

        // Remove the local snapshot of each culture. Cultures that no longer have any row in the database are not
        // listed here: their snapshot expires with the cache duration.
        foreach (var culture in entries.Select(e => e.Culture).Distinct())
        {
            await RemoveSnapshotFromCacheAsync(resource, culture);
        }

        // Publish resource-level invalidation with explicit list of keys so remote instances don't need to query DB
        if (publisher != null)
        {
            var invalidationKeys = entries
                .Select(e => new InvalidationKey(e.Key, e.Culture))
                .ToList();

            try
            {
                await publisher.PublishResourceInvalidationAsync(resource, invalidationKeys, cancellationToken);

                logger.LogInformation("Published resource invalidation for {Resource} with {Count} entries", resource, invalidationKeys.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish resource invalidation for {Resource}", resource);
            }
        }
    }
}
