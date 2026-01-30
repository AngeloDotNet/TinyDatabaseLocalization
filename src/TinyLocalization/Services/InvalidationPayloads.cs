namespace TinyLocalization.Services;

/// <summary>
/// Represents a unique key for invalidation, consisting of a key string and a culture identifier.
/// </summary>
/// <param name="Key">The unique identifier used for invalidation purposes.</param>
/// <param name="Culture">The culture associated with the key, which may influence localization or regional settings.</param>
public record InvalidationKey(string Key, string Culture);