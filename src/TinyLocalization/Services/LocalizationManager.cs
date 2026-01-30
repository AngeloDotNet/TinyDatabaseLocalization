using Microsoft.EntityFrameworkCore;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Services;

/// <summary>
/// Provides methods for managing localization resources, including adding, updating, removing, and invalidating
/// translations across different cultures. Ensures consistency between the underlying database and the cache for
/// localized content.
/// </summary>
/// <remarks>This class is designed for scenarios where localization data may be updated at runtime and must be
/// kept consistent across multiple application instances. It provides asynchronous operations to ensure that changes to
/// translations are reflected in both the database and the cache, and supports distributed cache invalidation when a
/// publisher is supplied.</remarks>
/// <param name="dbContext">The database context used to access and persist localization data.</param>
/// <param name="fusionCache">The cache instance used to store and retrieve localization resources for improved performance.</param>
/// <param name="options">The options that configure the behavior of the localization manager, such as cache key prefix settings.</param>
/// <param name="publisher">An optional publisher used to broadcast cache invalidation messages to other application instances. If not provided,
/// cache invalidation is performed only locally.</param>
public class LocalizationManager(LocalizationDbContext dbContext, IFusionCache fusionCache, DbLocalizationOptions options,
    ICacheInvalidationPublisher? publisher = null) : ILocalizationManager
{
    /// <summary>
    /// Generates a unique cache key that combines the specified resource identifier, key, and culture information.
    /// </summary>
    /// <remarks>Use this method to ensure that cache keys are consistently formatted and unique across
    /// different resources and cultures. This is particularly useful in localization scenarios where cache entries must
    /// be separated by culture.</remarks>
    /// <param name="resource">The resource identifier used to distinguish the cache entry. Cannot be null.</param>
    /// <param name="key">The unique key associated with the resource for cache retrieval. Cannot be null.</param>
    /// <param name="culture">The culture name used to differentiate cache entries for localization. If null or empty, a default segment is
    /// used.</param>
    /// <returns>A formatted string representing the complete cache key, including the cache key prefix, resource, key, and
    /// culture segment.</returns>
    private string BuildCacheKey(string resource, string key, string culture)
    {
        var cultureSegment = string.IsNullOrEmpty(culture) ? "_" : culture;
        return $"{options.CacheKeyPrefix}:{resource}:{key}:{cultureSegment}";
    }

    /// <summary>
    /// Adds a new translation or updates an existing translation asynchronously in the database for the specified
    /// resource, key, and culture.
    /// </summary>
    /// <remarks>If a translation with the same resource, key, and culture already exists, its value is
    /// updated; otherwise, a new translation is added. After the operation, the local cache for the affected
    /// translation is invalidated, and an invalidation message is published to other instances if a publisher is
    /// available.</remarks>
    /// <param name="translation">The translation to add or update. The resource, key, and culture identify the translation entry; the value is
    /// stored or updated.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous add or update operation.</returns>
    public async Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Translations.FirstOrDefaultAsync(t
            => t.Resource == translation.Resource && t.Key == translation.Key && t.Culture == translation.Culture, cancellationToken);

        if (existing == null)
        {
            dbContext.Translations.Add(translation);
        }
        else
        {
            existing.Value = translation.Value;
            dbContext.Translations.Update(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

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
    /// Removes a translation entry for the specified resource, key, and culture from the database asynchronously.
    /// </summary>
    /// <remarks>This method also invalidates the local cache for the removed translation and publishes an
    /// invalidation notification if a publisher is available. If no matching translation entry is found, no changes are
    /// made.</remarks>
    /// <param name="resource">The name of the resource associated with the translation entry to remove. Cannot be null.</param>
    /// <param name="key">The key that identifies the specific translation entry to remove. Cannot be null.</param>
    /// <param name="culture">The culture code that specifies which translation to remove. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the translation
    /// entry was found and removed; otherwise, <see langword="false"/>.</returns>
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

        // publish invalidation
        if (publisher != null)
        {
            await publisher.PublishSingleInvalidationAsync(resource, key, culture, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Asynchronously invalidates all cached entries for the specified resource across all known cultures.
    /// </summary>
    /// <remarks>This method removes all cache entries associated with the specified resource for each culture
    /// present in the data store. If a publisher is available, a resource-level invalidation notification is published
    /// after the cache entries are removed. Use this method to ensure that changes to a resource are reflected across
    /// all cultures and that outdated translations are not served from the cache.</remarks>
    /// <param name="resource">The name of the resource to invalidate. This parameter cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation of invalidating the resource.</returns>
    public async Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        // enumerate known cultures for this resource and remove corresponding keys
        var cultures = await dbContext.Translations
            .Where(t => t.Resource == resource)
            .Select(t => t.Culture)
            .Distinct()
            .ToListAsync(cancellationToken);

        var invalidationKeys = new List<InvalidationKey>();

        foreach (var culture in cultures)
        {
            // for each key too
            var keys = await dbContext.Translations
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