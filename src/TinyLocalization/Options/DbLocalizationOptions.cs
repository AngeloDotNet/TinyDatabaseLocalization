namespace TinyLocalization.Options;

/// <summary>
/// Options used to control database-backed localization behavior.
/// </summary>
/// <remarks>
/// These options configure caching, culture fallback behavior and how missing translations
/// are handled. They are intended to be bound from configuration or configured in code
/// when registering the localization services.
/// </remarks>
public class DbLocalizationOptions
{
    /// <summary>
    /// Default cache duration for translations stored in the in-memory cache.
    /// </summary>
    /// <remarks>
    /// The value is used to control how long a retrieved translation is kept in the
    /// cache (for example FusionCache) before it is refreshed from the underlying database.
    /// Default: 10 minutes.
    /// </remarks>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// When <see langword="true"/>, the localizer will attempt to fall back to parent cultures
    /// when a translation is not found for the exact current UI culture.
    /// </summary>
    /// <remarks>
    /// Example: if the current UI culture is "en-US" and a translation for a given key
    /// is not found, the localizer will try "en" (the parent culture) before giving up.
    /// Default: <c>true</c>.
    /// </remarks>
    public bool FallbackToParentCultures { get; set; } = true;

    /// <summary>
    /// Optional global fallback culture to use when lookups fail for the current and parent cultures.
    /// </summary>
    /// <remarks>
    /// Provide a culture name such as "en" to have the localizer attempt a final lookup
    /// using that culture when no translation is found for the current UI culture or its parents.
    /// If <see langword="null"/>, no global fallback is used.
    /// Default: <c>null</c>.
    /// </remarks>
    public string? GlobalFallbackCulture { get; set; } = null;

    /// <summary>
    /// Prefix used when composing cache keys for stored translations.
    /// </summary>
    /// <remarks>
    /// A prefix is useful to scope cache entries and to support targeted invalidation.
    /// Default: "db_localize".
    /// </remarks>
    public string CacheKeyPrefix { get; set; } = "db_localize";

    /// <summary>
    /// When <see langword="true"/>, the localizer will return the resource key itself when a translation
    /// is not found. This matches the default behavior of <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/>.
    /// </summary>
    /// <remarks>
    /// When set to <c>false</c>, consumers may receive <c>null</c> or an explicit indication that
    /// the resource was not found (behavior depends on the concrete localizer implementation).
    /// Default: <c>true</c>.
    /// </remarks>
    public bool ReturnKeyIfNotFound { get; set; } = true;
}