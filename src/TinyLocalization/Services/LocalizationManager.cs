using Microsoft.EntityFrameworkCore;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Services;

/// <summary>
/// Manager that modifies the translations database and invalidates associated FusionCache keys.
/// </summary>
/// <param name="db">The <see cref="LocalizationDbContext"/> used to access translations in the database.</param>
/// <param name="fusionCache">The <see cref="IFusionCache"/> used for local caching of translations.</param>
/// <param name="options">Options controlling localization behavior, including cache key prefix.</param>
/// <param name="publisher">Optional publisher used to broadcast cache invalidations to other instances.</param>
public class LocalizationManager(LocalizationDbContext db, IFusionCache fusionCache, DbLocalizationOptions options,
    ICacheInvalidationPublisher? publisher = null) : ILocalizationManager
{
    /// <summary>
    /// Builds the cache key used to store a translation in the cache.
    /// </summary>
    /// <param name="resource">The resource name the translation belongs to.</param>
    /// <param name="key">The translation key.</param>
    /// <param name="culture">
    /// The culture identifier for the translation. If <see cref="string.IsNullOrEmpty(string)"/> is true,
    /// an underscore ("_") segment is used to represent the empty culture in the key.
    /// </param>
    /// <returns>A string representing the full cache key for the given resource/key/culture combination.</returns>
    private string BuildCacheKey(string resource, string key, string culture)
    {
        var cultureSegment = string.IsNullOrEmpty(culture) ? "_" : culture;
        return $"{options.CacheKeyPrefix}:{resource}:{key}:{cultureSegment}";
    }

    /// <summary>
    /// Adds a new translation or updates an existing one in the database, then invalidates the corresponding cache entry.
    /// </summary>
    /// <param name="translation">The <see cref="Translation"/> entity to add or update.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// If a translation with the same resource, key and culture already exists it will be updated; otherwise a new entry
    /// will be added. After persisting changes it removes the matching FusionCache key locally and, when a publisher is
    /// provided, publishes a single-item invalidation to other instances.
    /// </remarks>
    public async Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default)
    {
        var existing = await db.Translations.FirstOrDefaultAsync(t
            => t.Resource == translation.Resource && t.Key == translation.Key && t.Culture == translation.Culture, cancellationToken);

        if (existing == null)
        {
            db.Translations.Add(translation);
        }
        else
        {
            existing.Value = translation.Value;
            db.Translations.Update(existing);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Invalidate cache for that exact resource/key/culture locally
        var cacheKey = BuildCacheKey(translation.Resource, translation.Key, translation.Culture);
        fusionCache.Remove(cacheKey, token: cancellationToken);

        // Publish invalidation to other instances (if publisher is available)
        if (publisher != null)
        {
            await publisher.PublishSingleInvalidationAsync(translation.Resource, translation.Key, translation.Culture, cancellationToken);
        }
    }

    /// <summary>
    /// Removes a translation for the specified resource/key/culture from the database and invalidates the cache.
    /// </summary>
    /// <param name="resource">The resource name the translation belongs to.</param>
    /// <param name="key">The translation key to remove.</param>
    /// <param name="culture">The culture identifier of the translation to remove.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that returns <c>true</c> if the translation was found and removed; otherwise <c>false</c> when no matching
    /// translation exists.
    /// </returns>
    /// <remarks>
    /// After removing the translation and saving changes the method removes the corresponding FusionCache entry locally
    /// and, if a publisher is configured, publishes a single-item invalidation to other instances.
    /// </remarks>
    public async Task<bool> RemoveAsync(string resource, string key, string culture, CancellationToken cancellationToken = default)
    {
        var existing = await db.Translations.FirstOrDefaultAsync(t
            => t.Resource == resource && t.Key == key && t.Culture == culture, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        db.Translations.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);

        // invalidate cache locally
        var cacheKey = BuildCacheKey(resource, key, culture);
        fusionCache.Remove(cacheKey, token: cancellationToken);

        // publish invalidation
        if (publisher != null)
        {
            await publisher.PublishSingleInvalidationAsync(resource, key, culture, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Invalidates all cached entries for a given resource across known cultures and keys.
    /// </summary>
    /// <param name="resource">The resource name whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The method enumerates the distinct cultures and keys for the resource in the database, removes the corresponding
    /// FusionCache entries locally, and collects <see cref="InvalidationKey"/> instances to publish a resource-level
    /// invalidation to other instances when a publisher is configured.
    /// </remarks>
    public async Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        // enumerate known cultures for this resource and remove corresponding keys
        var cultures = await db.Translations
            .Where(t => t.Resource == resource)
            .Select(t => t.Culture)
            .Distinct()
            .ToListAsync(cancellationToken);

        var invalidationKeys = new List<InvalidationKey>();

        foreach (var culture in cultures)
        {
            // for each key too
            var keys = await db.Translations
                .Where(t => t.Resource == resource && t.Culture == culture)
                .Select(t => t.Key)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var key in keys)
            {
                var cacheKey = BuildCacheKey(resource, key, culture);
                fusionCache.Remove(cacheKey, token: cancellationToken);

                // Collect invalidation keys for publishing
                invalidationKeys.Add(new InvalidationKey(key, culture));
            }
        }

        // Publish resource-level invalidation
        if (publisher != null)
        {
            await publisher.PublishResourceInvalidationAsync(resource, invalidationKeys, cancellationToken);
        }
    }
}