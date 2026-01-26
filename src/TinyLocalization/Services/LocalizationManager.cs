using Microsoft.EntityFrameworkCore;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Services;

/// <summary>
/// Manager responsible for creating, updating and removing translations in the database
/// and for evicting corresponding entries from the configured <see cref="IFusionCache"/>.
/// </summary>
/// <param name="dbContext">The <see cref="LocalizationDbContext"/> used to persist translations.</param>
/// <param name="fusionCache">The <see cref="IFusionCache"/> instance used to cache translation lookups.</param>
/// <param name="options">Configuration options controlling cache key prefixes and behavior.</param>
public class LocalizationManager(LocalizationDbContext dbContext, IFusionCache fusionCache, DbLocalizationOptions options) : ILocalizationManager
{
    /// <summary>
    /// Builds the cache key for a specific translation entry using the configured cache prefix, resource, key and culture.
    /// </summary>
    /// <param name="resource">Logical resource name that scopes translations (for example a full type name).</param>
    /// <param name="key">The translation key within the resource.</param>
    /// <param name="culture">
    /// The culture name (for example "en" or "en-US"). When empty or <see langword="null"/> the method uses "_" as the culture segment.
    /// </param>
    /// <returns>A string representing the composed cache key.</returns>
    private string BuildCacheKey(string resource, string key, string culture)
    {
        var cultureSegment = string.IsNullOrEmpty(culture) ? "_" : culture;

        return $"{options.CacheKeyPrefix}:{resource}:{key}:{cultureSegment}";
    }

    /// <summary>
    /// Adds a new translation or updates an existing translation in the database, then evicts the corresponding cache entry.
    /// </summary>
    /// <param name="translation">The <see cref="Translation"/> entity to add or update (contains Resource, Key, Culture, Value).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the operation has finished and cache was invalidated.</returns>
    /// <remarks>
    /// The method attempts to find an existing translation by resource/key/culture. If found it updates the value;
    /// otherwise it adds a new entity. After saving changes it removes the specific cache entry for that resource/key/culture.
    /// </remarks>
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

        // Invalidate cache for that exact resource/key/culture
        var cacheKey = BuildCacheKey(translation.Resource, translation.Key, translation.Culture);
        fusionCache.Remove(cacheKey, token: cancellationToken);
    }

    /// <summary>
    /// Removes a translation entry for the specified resource/key/culture and evicts the corresponding cache entry.
    /// </summary>
    /// <param name="resource">The logical resource name that scopes translations.</param>
    /// <param name="key">The translation key to remove.</param>
    /// <param name="culture">The culture name of the entry to remove (for example "en" or "en-US").</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> that resolves to <c>true</c> if an entry was found and removed; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// If the translation exists it is removed from the database, changes are saved, and the specific cache key is removed.
    /// </remarks>
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

        // invalidate cache
        var cacheKey = BuildCacheKey(resource, key, culture);

        fusionCache.Remove(cacheKey, token: cancellationToken);
        {
            return true;
        }
    }

    /// <summary>
    /// Invalidates all cached entries for the specified resource by enumerating known cultures and keys.
    /// </summary>
    /// <param name="resource">The logical resource name whose cache entries should be invalidated.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes when all matching cache entries have been removed.</returns>
    /// <remarks>
    /// The method queries the database to discover distinct cultures for the resource and then for each culture
    /// discovers distinct keys. For each key/culture pair it composes the cache key and calls <see cref="IFusionCache.Remove(string, CancellationToken)"/>.
    /// </remarks>
    public async Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        // enumerate known cultures for this resource and remove corresponding keys
        var cultures = await dbContext.Translations.Where(t => t.Resource == resource)
            .Select(t => t.Culture).Distinct().ToListAsync(cancellationToken);

        foreach (var culture in cultures)
        {
            // for each key too
            var keys = await dbContext.Translations.Where(t => t.Resource == resource && t.Culture == culture)
                .Select(t => t.Key).Distinct().ToListAsync(cancellationToken);

            foreach (var key in keys)
            {
                var cacheKey = BuildCacheKey(resource, key, culture);
                fusionCache.Remove(cacheKey, token: cancellationToken);
            }
        }
    }
}