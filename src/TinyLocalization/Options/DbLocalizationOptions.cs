namespace TinyLocalization.Options;

/// <summary>
/// Represents configuration options for customizing database-backed localization behavior.
/// </summary>
/// <remarks>Use this class to control various aspects of localization, such as cache duration, fallback culture
/// handling, and behavior when translations are missing. Adjust these options to suit the localization requirements and
/// performance characteristics of your application.</remarks>
public class DbLocalizationOptions
{
    /// <summary>
    /// Gets or sets the duration for which cached localization data remains valid.
    /// </summary>
    /// <remarks>The default value is 10 minutes. Adjust this value to balance performance and data freshness
    /// based on how frequently localization data changes in your application.</remarks>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets a value indicating whether the system should fall back to parent cultures when resources for a
    /// specific culture are not found.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, the resource lookup process will attempt to use the
    /// parent culture's resources if the resources for the current culture are unavailable. This can help provide a
    /// more comprehensive localization experience by reducing missing resource errors.</remarks>
    public bool FallbackToParentCultures { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the global fallback culture to use for localization when a specific culture is not
    /// available.
    /// </summary>
    /// <remarks>If a requested resource is not found for the current culture, the application will attempt to
    /// use the culture specified by this property as a fallback. This is useful for providing a consistent default
    /// localization experience when certain resources are missing for specific cultures. The value should be a valid
    /// culture name, such as "en" or "fr-CA". If this property is null, no global fallback culture will be
    /// used.</remarks>
    public string? GlobalFallbackCulture { get; set; } = null;

    /// <summary>
    /// Gets or sets the prefix used for cache keys in the localization database.
    /// </summary>
    /// <remarks>This property allows customization of the cache key prefix, which can be useful for
    /// distinguishing between different cache entries in scenarios where multiple applications or environments share
    /// the same cache store.</remarks>
    public string CacheKeyPrefix { get; set; } = "db_localize";

    /// <summary>
    /// Gets or sets a value indicating whether the key should be returned when a requested item is not found.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, the property ensures that the key itself is returned if
    /// the corresponding value does not exist. This can be useful in scenarios where the presence of the key is
    /// required for further processing, even if no value is available.</remarks>
    public bool ReturnKeyIfNotFound { get; set; } = true;
}