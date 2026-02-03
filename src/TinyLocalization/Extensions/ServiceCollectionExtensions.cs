using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;
using TinyLocalization.Enums;
using TinyLocalization.Localization;
using TinyLocalization.Options;
using TinyLocalization.Services;
using TinyLocalization.Services.Interfaces;
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
    /// Adds database-backed localization services to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddDbLocalization(this IServiceCollection services)
    {
        services.AddSingleton<IStringLocalizerFactory, EfStringLocalizerFactory>();
        services.AddTransient(typeof(IStringLocalizer<>), typeof(EfStringLocalizerGeneric<>));

        return services;
    }

    /// <summary>
    /// Adds services required for database-backed localization to the specified service collection, with optional
    /// configuration of localization options.
    /// </summary>
    /// <remarks>This method registers the necessary services for database localization, including
    /// implementations of <see cref="IStringLocalizerFactory"/>, <see cref="IStringLocalizer{T}"/>, and <see
    /// cref="ILocalizationManager"/>. The hosting application is expected to register <c>IFusionCache</c> and
    /// <c>LocalizationDbContext</c> separately for full functionality.</remarks>
    /// <param name="services">The service collection to which the localization services will be added. Cannot be null.</param>
    /// <param name="configuration">An optional delegate to configure the <see cref="DbLocalizationOptions"/> instance used for localization.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so that additional calls can be chained.</returns>
    public static IServiceCollection AddDbLocalization(this IServiceCollection services, Action<DbLocalizationOptions>? configuration = null)
    {
        var dbLocalizationOptions = new DbLocalizationOptions();
        configuration?.Invoke(dbLocalizationOptions);

        // register options instance
        services.TryAddSingleton(dbLocalizationOptions);

        // Register our factory as the IStringLocalizerFactory
        services.AddSingleton<IStringLocalizerFactory, EfStringLocalizerFactory>();

        // Register open-generic wrapper so IStringLocalizer<T> is available for injection
        services.AddTransient(typeof(IStringLocalizer<>), typeof(EfStringLocalizerGeneric<>));

        // Register localization manager for updates/invalidation
        services.AddScoped<ILocalizationManager, LocalizationManager>();

        // Expect the hosting app to register IFusionCache and LocalizationDbContext
        return services;
    }

    /// <summary>
    /// Configures and registers a localization database context of the specified type with provider-specific options in
    /// the dependency injection container.
    /// </summary>
    /// <remarks>This method enables localization support by configuring the specified DbContext with the
    /// appropriate database provider and options. It supports SQLite and SQL Server providers, and applies additional
    /// configuration such as migrations assembly, query splitting behavior, and logging options based on the provided
    /// DatabaseContextOptions.</remarks>
    /// <typeparam name="TContext">The type of the DbContext to configure for localization. Must inherit from DbContext.</typeparam>
    /// <param name="services">The IServiceCollection to which the localization DbContext and related services will be added.</param>
    /// <param name="configuration">An optional delegate to configure the database context options, such as database type, connection string, and
    /// provider-specific settings.</param>
    /// <returns>The IServiceCollection instance with the localization DbContext configured for further chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if the database type is set to 'None' or if the connection string is null, empty, or consists only of
    /// white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified database type is not supported by the method.</exception>
    public static IServiceCollection AddLocalizationDbContext<TContext>(this IServiceCollection services, Action<DatabaseContextOptions>? configuration = null) where TContext : DbContext
    {
        var databaseContextOptions = new DatabaseContextOptions();
        configuration?.Invoke(databaseContextOptions);

        services.TryAddSingleton(databaseContextOptions);

        if (databaseContextOptions.DatabaseType == DatabaseType.None)
        {
            throw new ArgumentException("Database type cannot be 'None' when configuring LocalizationDbContext.");
        }

        if (string.IsNullOrWhiteSpace(databaseContextOptions.ConnectionString))
        {
            throw new ArgumentException("Connection string must be provided when configuring LocalizationDbContext.");
        }

        var migrationsAssembly = databaseContextOptions.MigrationsAssembly ?? typeof(TContext).Assembly.FullName;

        services.AddDbContext<LocalizationDbContext>(builder =>
        {
            // provider-specific configuration
            switch (databaseContextOptions.DatabaseType)
            {
                case DatabaseType.SQLite:
                    builder.UseSqlite(databaseContextOptions.ConnectionString, sql =>
                    {
                        sql.MigrationsAssembly(migrationsAssembly);
                        sql.MigrationsHistoryTable(databaseContextOptions.MigrationsHistoryTable);

                        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    break;

                case DatabaseType.SqlServer:
                    builder.UseSqlServer(databaseContextOptions.ConnectionString, sql =>
                    {
                        sql.MigrationsAssembly(migrationsAssembly);
                        sql.MigrationsHistoryTable(databaseContextOptions.MigrationsHistoryTable);

                        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        sql.UseCompatibilityLevel(databaseContextOptions.SQLServerCompatibilityLevel);
                    });
                    break;

                case DatabaseType.PostgreSQL:
                    builder.UseNpgsql(databaseContextOptions.ConnectionString, sql =>
                    {
                        sql.MigrationsAssembly(migrationsAssembly);
                        sql.MigrationsHistoryTable(databaseContextOptions.MigrationsHistoryTable);

                        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseContextOptions.DatabaseType), "Unsupported database type");
            }

            // optionally enable tracking for read-only scenarios (disabled by default)
            if (!databaseContextOptions.EnableUseQueryTrackingBehavior)
            {
                //builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                builder.UseQueryTrackingBehavior(databaseContextOptions.QueryTrackingBehavior);
            }

            // Log only command executed events (pass array of event ids)
            builder.LogTo(Console.WriteLine, [RelationalEventId.CommandExecuted]);

            // enable sensitive logging only if explicitly requested
            if (databaseContextOptions.EnableSensitiveDataLogging)
            {
                builder.EnableSensitiveDataLogging();
            }

            // optionally enable detailed errors if your options expose it
            if (databaseContextOptions.EnableDetailedErrors)
            {
                builder.EnableDetailedErrors();
            }
        });

        return services;
    }

    /// <summary>
    /// Configures and adds the default FusionCache services to the specified <see cref="IServiceCollection"/>, enabling
    /// customizable caching options for the application.
    /// </summary>
    /// <remarks>This method sets up FusionCache with default options, including memory cache durations,
    /// fail-safe settings, and eager refresh thresholds. The configuration delegate can be used to override these
    /// defaults to suit specific application requirements.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the FusionCache services will be registered.</param>
    /// <param name="configuration">An optional delegate to configure the <see cref="DefaultFusionCacheOptions"/>, allowing customization of cache
    /// behavior and settings.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance with FusionCache services registered.</returns>
    public static IServiceCollection AddDefaultFusionCache(this IServiceCollection services, Action<DefaultFusionCacheOptions>? configuration = null)
    {
        var fusionCacheOptions = new DefaultFusionCacheOptions();
        configuration?.Invoke(fusionCacheOptions);

        services.TryAddSingleton(fusionCacheOptions);

        services.AddFusionCache()
            .WithOptions(options =>
            {
                // Keep data in L1 for 5 minutes, then get it again from L2

                // This is for normal entries
                options.DefaultEntryOptions.MemoryCacheDuration = TimeSpan.FromMinutes(fusionCacheOptions.DefaultMemoryCacheDuration);

                // This is for tagging-related entries
                options.TagsDefaultEntryOptions.MemoryCacheDuration = TimeSpan.FromMinutes(fusionCacheOptions.TagsDefaultMemoryCacheDuration);
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(fusionCacheOptions.CacheDurationInMinutes),

                // Fail-safe options
                IsFailSafeEnabled = fusionCacheOptions.FailSafeModeEnabled,
                FailSafeMaxDuration = TimeSpan.FromMinutes(fusionCacheOptions.FailSafeMaxDurationInMinutes),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(fusionCacheOptions.FailSafeThrottleDurationInSeconds),

                // Eager refresh
                EagerRefreshThreshold = fusionCacheOptions.EagerRefreshThreshold,

                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactorySoftTimeoutInMilliseconds),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactoryHardTimeoutInMilliseconds)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer());

        return services;
    }

    /// <summary>
    /// Adds distributed FusionCache services to the specified <see cref="IServiceCollection"/>, enabling advanced
    /// caching capabilities with distributed and fail-safe options.
    /// </summary>
    /// <remarks>This method configures FusionCache with distributed caching support, including Redis
    /// integration and backplane functionality. It enables customization of cache durations, fail-safe mechanisms,
    /// eager refresh, and distributed cache timeouts. Use the <paramref name="configuration"/> parameter to tailor
    /// caching behavior to application requirements.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the distributed FusionCache services will be registered.</param>
    /// <param name="configuration">An optional delegate to configure <see cref="DistributedFusionCacheOptions"/>, allowing customization of cache
    /// behavior, durations, fail-safe settings, and distributed cache integration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, allowing for method chaining.</returns>
    public static IServiceCollection AddDistributedFusionCache(this IServiceCollection services, Action<DistributedFusionCacheOptions>? configuration = null)
    {
        var fusionCacheOptions = new DistributedFusionCacheOptions();
        configuration?.Invoke(fusionCacheOptions);

        services.TryAddSingleton(fusionCacheOptions);

        services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheCircuitBreakerDurationInMinutes);
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(fusionCacheOptions.CacheDurationInMinutes),

                // Fail-safe options
                IsFailSafeEnabled = fusionCacheOptions.FailSafeModeEnabled,
                FailSafeMaxDuration = TimeSpan.FromMinutes(fusionCacheOptions.FailSafeMaxDurationInMinutes),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(fusionCacheOptions.FailSafeThrottleDurationInSeconds),

                // Eager refresh
                EagerRefreshThreshold = fusionCacheOptions.EagerRefreshThreshold,
                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactorySoftTimeoutInMilliseconds),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactoryHardTimeoutInMilliseconds),

                // Distributed cache options
                DistributedCacheSoftTimeout = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheSoftTimeoutInMinutes),
                DistributedCacheHardTimeout = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheHardTimeoutInMinutes),
                AllowBackgroundDistributedCacheOperations = fusionCacheOptions.AllowBackgroundDistributedCacheOperationsEnabled,

                // Jittering
                JitterMaxDuration = TimeSpan.FromMinutes(fusionCacheOptions.JitterMaxDurationInMinutes)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            // Add Redis distributed cache support
            .WithDistributedCache(new RedisCache(new RedisCacheOptions() { Configuration = fusionCacheOptions.RedisCacheConnectionString }))
            // Add the fusion cache backplane for Redis
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions() { Configuration = fusionCacheOptions.RedisBackplaneConnectionString }));

        return services;
    }

    /// <summary>
    /// Registers the distributed logs FusionCache services with the specified configuration options.
    /// </summary>
    /// <remarks>This method configures FusionCache with custom cache durations, fail-safe settings, and
    /// logging levels. It also integrates Redis for distributed caching and backplane support. Use the configuration
    /// parameter to customize cache behavior and connection settings as needed.</remarks>
    /// <param name="services">The service collection to which the distributed logs FusionCache services will be added.</param>
    /// <param name="configuration">An optional action to configure the settings of the distributed logs FusionCache. If not specified, default
    /// options are used.</param>
    /// <returns>The updated service collection with the distributed logs FusionCache services registered.</returns>
    public static IServiceCollection AddDistributedLogsFusionCache(this IServiceCollection services, Action<DistributedLogsFusionCacheOptions>? configuration = null)
    {
        var fusionCacheOptions = new DistributedLogsFusionCacheOptions();
        configuration?.Invoke(fusionCacheOptions);

        services.TryAddSingleton(fusionCacheOptions);

        services.AddFusionCache()
            .WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheCircuitBreakerDurationInMinutes);

                // Custom log levels
                options.FailSafeActivationLogLevel = fusionCacheOptions.FailSafeActivationLogLevel;
                options.SerializationErrorsLogLevel = fusionCacheOptions.SerializationErrorsLogLevel;

                options.DistributedCacheSyntheticTimeoutsLogLevel = fusionCacheOptions.DistributedCacheSyntheticTimeoutsLogLevel;
                options.DistributedCacheErrorsLogLevel = fusionCacheOptions.DistributedCacheErrorsLogLevel;

                options.FactorySyntheticTimeoutsLogLevel = fusionCacheOptions.FactorySyntheticTimeoutsLogLevel;
                options.FactoryErrorsLogLevel = fusionCacheOptions.FactoryErrorsLogLevel;
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                // Cache duration
                Duration = TimeSpan.FromMinutes(fusionCacheOptions.CacheDurationInMinutes),

                // Fail-safe options
                IsFailSafeEnabled = true,
                FailSafeMaxDuration = TimeSpan.FromHours(fusionCacheOptions.FailSafeMaxDurationInMinutes),
                FailSafeThrottleDuration = TimeSpan.FromSeconds(fusionCacheOptions.FailSafeThrottleDurationInSeconds),

                // Eager refresh
                EagerRefreshThreshold = fusionCacheOptions.EagerRefreshThreshold,

                // Factory timeouts
                FactorySoftTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactorySoftTimeoutInMilliseconds),
                FactoryHardTimeout = TimeSpan.FromMilliseconds(fusionCacheOptions.FactoryHardTimeoutInMilliseconds),

                // Distributed cache options
                DistributedCacheSoftTimeout = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheSoftTimeoutInMinutes),
                DistributedCacheHardTimeout = TimeSpan.FromMinutes(fusionCacheOptions.DistributedCacheHardTimeoutInMinutes),
                AllowBackgroundDistributedCacheOperations = fusionCacheOptions.AllowBackgroundDistributedCacheOperationsEnabled,

                // Jittering
                JitterMaxDuration = TimeSpan.FromMinutes(fusionCacheOptions.JitterMaxDurationInMinutes)
            })
            // Add FusionCache serialization based on System.Text.Json
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            // Add Redis distributed cache support
            .WithDistributedCache(new RedisCache(new RedisCacheOptions() { Configuration = fusionCacheOptions.RedisCacheConnectionString }))
            // Add the fusion cache backplane for Redis
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions() { Configuration = fusionCacheOptions.RedisBackplaneConnectionString }));

        return services;
    }
}