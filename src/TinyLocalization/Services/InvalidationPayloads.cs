namespace TinyLocalization.Services;

/// <summary>
/// Payload element used for resource-level invalidation: a pair containing a translation <see cref="Key"/>
/// and the target <see cref="Culture"/>. Instances of this record are typically included in resource-level
/// invalidation messages to indicate which specific keys and cultures should be evicted or refreshed by subscribers.
/// </summary>
/// <param name="Key">The translation key within the resource to be invalidated.</param>
/// <param name="Culture">The culture name (for example "en" or "en-US") whose cached entry should be invalidated.</param>
public record InvalidationKey(string Key, string Culture);