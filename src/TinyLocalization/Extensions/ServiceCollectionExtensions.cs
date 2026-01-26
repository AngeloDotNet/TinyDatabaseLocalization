using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using TinyLocalization.Data;
using TinyLocalization.Localization;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace TinyLocalization.Extensions;

/// <summary>
/// Provides extension methods to register localization services and configure FusionCache instances.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF-based localization factory and the generic localizer wrapper.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The modified <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <remarks>
    /// This method registers <see cref="EfStringLocalizerFactory"/> as the implementation of
    /// <see cref="IStringLocalizerFactory"/> and registers an open-generic
    /// <see cref="IStringLocalizer{T}"/> mapping to <see cref="EfStringLocalizerGeneric{T}"/>.
    /// The caller is still responsible for registering the <see cref="LocalizationDbContext"/> (for example
    /// via <see cref="AddLocalizationDbContextSqlite"/> or a custom registration).
    /// </remarks>
    public static IServiceCollection AddDbLocalization(this IServiceCollection services)
        => services.AddSingleton<IStringLocalizerFactory, EfStringLocalizerFactory>()
            .AddTransient(typeof(IStringLocalizer<>), typeof(EfStringLocalizerGeneric<>));

    /// <summary>
    /// Adds the <see cref="LocalizationDbContext"/> configured to use SQLite.
    /// </summary>
    /// <param name="services">The service collection to add the DbContext to.</param>
    /// <param name="connectionString">The SQLite connection string to use when configuring the DbContext.</param>
    /// <returns>The modified <see cref="IServiceCollection"/> instance for chaining.</returns>
    /// <remarks>
    /// Convenience helper that calls <c>options.UseSqlite(connectionString)</c> when registering
    /// <see cref="LocalizationDbContext"/>. Use this in development or lightweight scenarios; for production,
    /// the caller may prefer a different DbContext configuration.
    /// </remarks>
    public static IServiceCollection AddLocalizationDbContextSqlite(this IServiceCollection services, string connectionString)
        => services.AddDbContext<LocalizationDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            options.LogTo(Console.WriteLine, [RelationalEventId.CommandExecuted]);
            options.EnableSensitiveDataLogging();
        });

    /// <summary>
    /// Configures a FusionCache instance with sensible default in-memory options and System.Text.Json serialization.
    /// </summary>
    /// <param name="services">The Fusion cache builder to configure.</param>
    /// <returns>The modified <see cref="IFusionCacheBuilder"/> for chaining.</returns>
    /// <remarks>
    /// - Sets L1 memory cache durations to 5 minutes for default and tags entries.
    /// - Sets a default entry duration of 10 minutes and enables fail-safe behavior with a max duration of 2 hours.
    /// - Configures eager refresh, factory timeouts and a System.Text.Json serializer.
    /// This helper does not add any distributed cache/backplane; use the distributed helpers for Redis integration.
    /// </remarks>
    public static IFusionCacheBuilder AddFusionCache(this IFusionCacheBuilder services)
        => services.AddFusionCache()
            .WithOptions(options =>
            {
                // Keep data in L1 for 5 minutes, then get it again from L2
                options.DefaultEntryOptions.MemoryCacheDuration = TimeSpan.FromMinutes(5); // This is for normal entries                
                options.TagsDefaultEntryOptions.MemoryCacheDuration = TimeSpan.FromMinutes(5); // This is for tagging-related entries
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(10),

                // Fail-safe options
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

                // Eager refresh
                EagerRefreshThreshold = 0.9f,

                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(1500)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer());

    /// <summary>
    /// Configures a FusionCache instance with Redis as distributed cache and Redis-based backplane.
    /// </summary>
    /// <param name="services">The Fusion cache builder to configure.</param>
    /// <param name="redisConnectionString">The connection string used to configure Redis for distributed cache and backplane.</param>
    /// <returns>The modified <see cref="IFusionCacheBuilder"/> for chaining.</returns>
    /// <remarks>
    /// - Enables a distributed cache circuit breaker duration of 2 minutes.
    /// - Configures default entry options similar to <see cref="AddFusionCache"/> plus distributed cache timeouts and jittering.
    /// - Adds a Redis distributed cache (<see cref="RedisCache"/>) and a Redis backplane (<see cref="RedisBackplane"/>).
    /// Use this helper when you want FusionCache to use Redis as a shared L2 cache and to propagate invalidation messages.
    /// </remarks>
    public static IFusionCacheBuilder AddFusionDistributedCache(this IFusionCacheBuilder services, string redisConnectionString)
        => services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromMinutes(2);
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(10),

                // Fail-safe options
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

                // Eager refresh
                EagerRefreshThreshold = 0.9f,

                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(1500),

                // Distributed cache options
                DistributedCacheSoftTimeout = TimeSpan.FromMinutes(10),
                DistributedCacheHardTimeout = TimeSpan.FromMinutes(20),
                AllowBackgroundDistributedCacheOperations = true,

                // Jittering
                JitterMaxDuration = TimeSpan.FromMinutes(2)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            // Add Redis distributed cache support
            .WithDistributedCache(new RedisCache(new RedisCacheOptions() { Configuration = redisConnectionString }))
            // Add the fusion cache backplane for Redis
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions() { Configuration = redisConnectionString }));

    /// <summary>
    /// Configures a FusionCache instance with Redis distributed cache and backplane and enables custom log levels.
    /// </summary>
    /// <param name="services">The Fusion cache builder to configure.</param>
    /// <param name="redisConnectionString">The connection string used to configure Redis for distributed cache and backplane.</param>
    /// <returns>The modified <see cref="IFusionCacheBuilder"/> for chaining.</returns>
    /// <remarks>
    /// This helper is similar to <see cref="AddFusionDistributedCache"/> but also customizes logging levels for:
    /// - Fail-safe activation and serialization errors
    /// - Distributed cache synthetic timeouts and errors
    /// - Factory synthetic timeouts and errors
    /// It still configures default entry options, distributed cache settings and adds Redis-based distributed cache and backplane.
    /// </remarks>
    public static IFusionCacheBuilder AddFusionDistributedCacheWithLogs(this IFusionCacheBuilder services, string redisConnectionString)
        => services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromMinutes(2);

                // Custom log levels
                options.FailSafeActivationLogLevel = LogLevel.Debug;
                options.SerializationErrorsLogLevel = LogLevel.Warning;

                options.DistributedCacheSyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.DistributedCacheErrorsLogLevel = LogLevel.Error;

                options.FactorySyntheticTimeoutsLogLevel = LogLevel.Debug;
                options.FactoryErrorsLogLevel = LogLevel.Error;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(10),

                // Fail-safe options
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(2),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

                // Eager refresh
                EagerRefreshThreshold = 0.9f,

                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(1500),

                // Distributed cache options
                DistributedCacheSoftTimeout = TimeSpan.FromMinutes(10),
                DistributedCacheHardTimeout = TimeSpan.FromMinutes(20),
                AllowBackgroundDistributedCacheOperations = true,

                // Jittering
                JitterMaxDuration = TimeSpan.FromMinutes(2)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            // Add Redis distributed cache support
            .WithDistributedCache(new RedisCache(new RedisCacheOptions() { Configuration = redisConnectionString }))
            // Add the fusion cache backplane for Redis
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions() { Configuration = redisConnectionString }));
}