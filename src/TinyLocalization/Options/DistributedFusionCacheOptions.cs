namespace TinyLocalization.Options;

/// <summary>
/// Represents the configuration options for distributed fusion cache, allowing customization of caching behavior,
/// timeouts, fail-safe mechanisms, and distributed cache integration settings.
/// </summary>
/// <remarks>Use this class to configure how the distributed fusion cache operates, including circuit breaker
/// durations, cache expiration policies, fail-safe strategies, and distributed cache connectivity. Adjust these options
/// to optimize cache reliability, performance, and integration with distributed systems such as Redis. Proper
/// configuration is essential to ensure the cache meets the application's consistency, availability, and performance
/// requirements.</remarks>
public class DistributedFusionCacheOptions
{
    /// <summary>
    /// Gets or sets the duration, in minutes, that the circuit breaker remains open after a failure in the distributed
    /// cache system.
    /// </summary>
    /// <remarks>This property determines how long the circuit breaker will prevent further requests to the
    /// distributed cache after a failure occurs. Setting this value appropriately can help balance system resilience
    /// and recovery time. A value of 2 means the circuit breaker will remain open for 2 minutes before allowing
    /// requests to be retried.</remarks>
    public int DistributedCacheCircuitBreakerDurationInMinutes { get; set; } = 2;

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
    /// corruption during critical operations. It is recommended to keep this mode enabled unless specific performance
    /// optimizations are required.</remarks>
    public bool FailSafeModeEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration, in minutes, that fail-safe operations are allowed to run before being
    /// terminated.
    /// </summary>
    /// <remarks>If a fail-safe process exceeds this duration, the system may trigger mechanisms to halt the
    /// operation and prevent potential issues. Adjust this value based on the expected duration of typical fail-safe
    /// activities in your environment.</remarks>
    public int FailSafeMaxDurationInMinutes { get; set; } = 120;

    /// <summary>
    /// Gets or sets the duration, in seconds, that the system remains in a fail-safe throttled state before resuming
    /// normal operations.
    /// </summary>
    /// <remarks>Adjusting this value can help control how quickly the system recovers from a fail-safe
    /// condition, which may be useful for managing responsiveness during periods of high load or transient
    /// failures.</remarks>
    public int FailSafeThrottleDurationInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the threshold, as a fractional value between 0.0 and 1.0, at which an eager refresh operation is
    /// triggered.
    /// </summary>
    /// <remarks>This property determines when the system initiates an eager refresh based on the specified
    /// threshold. Setting a lower value causes refreshes to occur sooner, while a higher value delays them until closer
    /// to expiration. Adjust this value to balance responsiveness and resource usage according to application
    /// needs.</remarks>
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
    /// exceeds this duration, it will be stopped to prevent excessive resource usage or potential deadlocks. The
    /// default value is 1500 milliseconds. Adjust this value based on the expected execution time and performance
    /// requirements of your factory operations.</remarks>
    public int FactoryHardTimeoutInMilliseconds { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the soft timeout duration, in minutes, for items stored in the distributed cache.
    /// </summary>
    /// <remarks>This property determines how long cached items remain valid before they are considered
    /// expired. A value of 0 or less indicates that items do not expire.</remarks>
    public int DistributedCacheSoftTimeoutInMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the hard timeout duration, in minutes, for items stored in the distributed cache.
    /// </summary>
    /// <remarks>This property defines the maximum time that cached items are retained before they are
    /// considered expired and removed from the distributed cache. Adjust this value to balance cache freshness and
    /// resource usage based on application requirements.</remarks>
    public int DistributedCacheHardTimeoutInMinutes { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether background operations for distributed cache are enabled.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, background operations for distributed cache can be
    /// performed, which may improve performance by allowing asynchronous processing. If set to <see langword="false"/>,
    /// such operations will be disabled, potentially impacting the efficiency of cache management.</remarks>
    public bool AllowBackgroundDistributedCacheOperationsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration, in minutes, for which jitter can be applied to operations.
    /// </summary>
    /// <remarks>Configuring this property allows operations to be distributed over time, which can help
    /// reduce contention or avoid overloading resources. The default value is 2 minutes.</remarks>
    public int JitterMaxDurationInMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets the connection string used to connect to the Redis cache instance.
    /// </summary>
    /// <remarks>Ensure that the connection string is correctly formatted according to the Redis server
    /// requirements. An invalid or incomplete connection string may result in connection failures or unexpected
    /// behavior.</remarks>
    public string RedisCacheConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string used to connect to the Redis backplane.
    /// </summary>
    /// <remarks>This connection string is essential for configuring the Redis backplane, which facilitates
    /// message distribution across multiple servers in a distributed application. Ensure that the connection string is
    /// correctly formatted to establish a successful connection.</remarks>
    public string RedisBackplaneConnectionString { get; set; } = string.Empty;
}