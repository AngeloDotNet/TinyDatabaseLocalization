using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Services;

/// <summary>
/// Provides methods for managing localization resources, including adding, updating, removing, and invalidating
/// translations in both the database and cache.
/// </summary>
/// <remarks>The LocalizationManager ensures that changes to translations are reflected in both the underlying
/// database and the distributed cache. When a translation is added, updated, or removed, the corresponding cache entry
/// is invalidated locally, and, if a publisher is provided, invalidation messages are sent to other instances to
/// maintain consistency across distributed environments.</remarks>
/// <param name="dbContext">The database context used to access and persist localization data.</param>
/// <param name="fusionCache">The cache instance used to store and retrieve localization resources for improved performance.</param>
/// <param name="options">The configuration options that control localization manager behavior, such as cache key prefix and other settings.</param>
/// <param name="logger">The logger used to record informational and warning messages related to localization operations.</param>
/// <param name="publisher">An optional publisher for broadcasting cache invalidation messages to other application instances.</param>
public class LocalizationManager(LocalizationDbContext dbContext, IFusionCache fusionCache, DbLocalizationOptions options, ILogger<LocalizationManager> logger,
    ICacheInvalidationPublisher? publisher = null) : ILocalizationManager
{
    private string BuildCacheKey(string resource, string key, string culture)
    {
        var cultureSegment = string.IsNullOrEmpty(culture) ? "_" : culture;
        return $"{options.CacheKeyPrefix}:{resource}:{key}:{cultureSegment}";
    }

    public async Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Translations.FirstOrDefaultAsync(t
            => t.Resource == translation.Resource && t.Key == translation.Key && t.Culture == translation.Culture, cancellationToken);

        if (existing == null)
        {
            dbContext.Translations.Add(translation);

            logger.LogInformation("Added translation {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
        }
        else
        {
            existing.Value = translation.Value;
            dbContext.Translations.Update(existing);

            logger.LogInformation("Updated translation {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache for that exact resource/key/culture locally
        var cacheKey = BuildCacheKey(translation.Resource, translation.Key, translation.Culture);
        fusionCache.Remove(cacheKey, token: cancellationToken);

        logger.LogDebug("Removed local cache key {CacheKey}", cacheKey);

        // Publish invalidation to other instances (if publisher is available)
        if (publisher != null)
        {
            try
            {
                await publisher.PublishSingleInvalidationAsync(translation.Resource, translation.Key, translation.Culture, cancellationToken);

                logger.LogInformation("Published single invalidation for {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish single invalidation for {Resource}/{Key}/{Culture}", translation.Resource, translation.Key, translation.Culture);
            }
        }
    }

    public async Task<bool> RemoveAsync(string resource, string key, string culture, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Translations.FirstOrDefaultAsync(t
            => t.Resource == resource && t.Key == key && t.Culture == culture, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        dbContext.Translations.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        // invalidate cache locally
        var cacheKey = BuildCacheKey(resource, key, culture);
        fusionCache.Remove(cacheKey, token: cancellationToken);

        logger.LogInformation("Removed translation and local cache for {Resource}/{Key}/{Culture}", resource, key, culture);

        // publish invalidation
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

        return true;
    }

    public async Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        // enumerate known keys+cultures for this resource so we can invalidate locally and publish the exact list
        var entries = await dbContext.Translations
            .Where(t => t.Resource == resource)
            .Select(t => new { t.Key, t.Culture })
            .ToListAsync(cancellationToken);

        var invalidationKeys = entries
            .Select(e => new InvalidationKey(e.Key, e.Culture))
            .ToList()
            .AsEnumerable();

        // Remove local cache entries for each
        foreach (var ik in invalidationKeys)
        {
            var cacheKey = BuildCacheKey(resource, ik.Key, ik.Culture);
            fusionCache.Remove(cacheKey, token: cancellationToken);

            logger.LogDebug("Removed local cache key {CacheKey} during resource invalidation", cacheKey);
        }

        // Publish resource-level invalidation with explicit list of keys so remote instances don't need to query DB
        if (publisher != null)
        {
            try
            {
                await publisher.PublishResourceInvalidationAsync(resource, invalidationKeys, cancellationToken);

                logger.LogInformation("Published resource invalidation for {Resource} with {Count} entries", resource, invalidationKeys.Count());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish resource invalidation for {Resource}", resource);
            }
        }
    }
}