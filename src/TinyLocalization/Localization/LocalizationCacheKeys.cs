namespace TinyLocalization.Localization;

/// <summary>
/// Builds the cache keys used by <see cref="EfStringLocalizer"/> and <c>LocalizationManager</c>.
/// </summary>
/// <remarks>Keeping the key format in a single place guarantees that the code which stores a cache entry and the
/// code which invalidates it (including any custom invalidation subscriber) always agree on the key. The type is
/// public so that custom <c>ICacheInvalidationPublisher</c> subscribers can reuse it.</remarks>
public static class LocalizationCacheKeys
{
    /// <summary>
    /// The segment used in cache keys to represent the invariant culture (empty culture name).
    /// </summary>
    public const string InvariantCultureSegment = "_";

    /// <summary>
    /// Builds the cache key of the snapshot that holds all the translations of a resource for a single culture.
    /// </summary>
    /// <param name="prefix">The cache key prefix, see <c>DbLocalizationOptions.CacheKeyPrefix</c>.</param>
    /// <param name="resource">The resource name.</param>
    /// <param name="culture">The culture name. An empty string identifies the invariant culture.</param>
    /// <returns>The cache key for the (resource, culture) snapshot.</returns>
    /// <remarks>The culture is always the last segment and culture names never contain ':', so the mapping from
    /// (resource, culture) to key is unambiguous even when the resource name contains ':'.</remarks>
    public static string ForSnapshot(string prefix, string resource, string culture)
    {
        var cultureSegment = string.IsNullOrEmpty(culture) ? InvariantCultureSegment : culture;

        return $"{prefix}:snap:{resource}:{cultureSegment}";
    }
}
