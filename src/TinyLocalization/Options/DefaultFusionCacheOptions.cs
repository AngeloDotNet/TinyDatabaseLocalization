namespace TinyLocalization.Options;

/// <summary>
/// Provides configuration options for the default fusion cache behavior, including memory cache durations and fail-safe
/// settings.
/// </summary>
/// <remarks>This class allows customization of cache durations and fail-safe mechanisms to optimize caching
/// strategies in applications. The default values are set to ensure a reasonable starting point for most use
/// cases.</remarks>
public class DefaultFusionCacheOptions
{
    /// <summary>
    /// Gets or sets the default duration, in minutes, for which items are cached in memory.
    /// </summary>
    /// <remarks>Adjusting this value allows you to control how long cached items remain available before they
    /// expire and are removed from memory. Setting an appropriate duration can help balance memory usage and
    /// application performance based on your caching needs.</remarks>
    public int DefaultMemoryCacheDuration { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration, in minutes, for which tags are cached in memory.
    /// </summary>
    /// <remarks>This property determines how long the tags will remain in the memory cache before they are
    /// considered stale and removed. Adjusting this value can help optimize performance based on the application's
    /// caching strategy.</remarks>
    public int TagsDefaultMemoryCacheDuration { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration, in minutes, for which the cache remains valid before it expires.
    /// </summary>
    /// <remarks>The default value is 10 minutes. Adjust this value to optimize performance based on how
    /// frequently the underlying data changes and how often it is accessed.</remarks>
    public int CacheDurationInMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether fail-safe mode is enabled.
    /// </summary>
    /// <remarks>When enabled, fail-safe mode provides additional safeguards to prevent data loss or
    /// corruption during critical operations. This mode is typically used in scenarios where data integrity is
    /// paramount.</remarks>
    public bool FailSafeModeEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration, in minutes, that fail-safe operations are allowed to run before being
    /// terminated.
    /// </summary>
    /// <remarks>Set this value according to the application's requirements for responsiveness and
    /// reliability. A lower value may improve responsiveness by limiting the time spent in fail-safe mode, while a
    /// higher value may provide greater tolerance for transient failures.</remarks>
    public int FailSafeMaxDurationInMinutes { get; set; } = 120;

    /// <summary>
    /// Gets or sets the duration, in seconds, that the system remains in a fail-safe throttled state before resuming
    /// normal operations.
    /// </summary>
    /// <remarks>Adjust this value to control how long the system delays recovery after a fail-safe condition
    /// is triggered. Increasing the duration can help prevent repeated failures during high-load scenarios, while
    /// decreasing it allows for quicker recovery.</remarks>
    public int FailSafeThrottleDurationInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the threshold at which an eager refresh operation is triggered.
    /// </summary>
    /// <remarks>This value represents a fraction between 0.0 and 1.0 that determines when a refresh should
    /// occur before the underlying data expires. Setting a lower value causes refreshes to happen earlier, potentially
    /// improving responsiveness at the cost of more frequent refresh operations.</remarks>
    public float EagerRefreshThreshold { get; set; } = 0.9f;

    /// <summary>
    /// Gets or sets the soft timeout duration, in milliseconds, for factory operations.
    /// </summary>
    /// <remarks>This property defines the maximum time allowed for factory operations to complete before
    /// timing out. A value of 100 milliseconds is set by default.</remarks>
    public int FactorySoftTimeoutInMilliseconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum duration, in milliseconds, that a factory operation is allowed to run before it is
    /// forcibly terminated.
    /// </summary>
    /// <remarks>This property defines a hard timeout limit for factory operations. If a factory operation
    /// exceeds this duration, it will be stopped to prevent excessive delays. The default value is 1500 milliseconds.
    /// Adjust this value based on the expected performance and responsiveness requirements of your
    /// application.</remarks>
    public int FactoryHardTimeoutInMilliseconds { get; set; } = 1500;
}
