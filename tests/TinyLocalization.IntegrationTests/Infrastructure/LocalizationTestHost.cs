using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;
using TinyLocalization.Entities;
using TinyLocalization.Extensions;
using TinyLocalization.Options;
using TinyLocalization.Services.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.IntegrationTests.Infrastructure;

/// <summary>
/// A self-contained application host for one test: real dependency injection container, real FusionCache (memory
/// only) and a real SQLite database stored in a temporary file that is deleted when the host is disposed.
/// </summary>
/// <remarks>Every test creates its own host, so tests never share cache or database state and can run in parallel.
/// The DbContext uses NoTracking by default, exactly like the library configuration does.</remarks>
internal sealed class LocalizationTestHost : IAsyncDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"tinylocalization-tests-{Guid.NewGuid():N}.db");

    public LocalizationTestHost(Action<DbLocalizationOptions>? configureOptions = null, Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddFusionCache();

        services.AddDbContext<LocalizationDbContext>(builder => builder
            .UseSqlite($"Data Source={databasePath}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(Queries, SaveHook));

        services.AddDatabaseLocalization(configureOptions);
        configureServices?.Invoke(services);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LocalizationDbContext>().Database.EnsureCreated();

        Queries.Reset();
    }

    public ServiceProvider Services { get; }

    /// <summary>
    /// Counts the queries sent to the database, to verify how often the localizer really hits it.
    /// </summary>
    public QueryCounter Queries { get; } = new();

    /// <summary>
    /// Lets a test run code right before the next SaveChanges of any context, to reproduce a race deterministically.
    /// </summary>
    public SaveHook SaveHook { get; } = new();

    /// <summary>
    /// Gets the localizer for a resource through the same factory the application uses.
    /// </summary>
    public IStringLocalizer Localizer(string resource = "Shared")
        => Services.GetRequiredService<IStringLocalizerFactory>().Create(resource, string.Empty);

    /// <summary>
    /// Inserts rows directly in the database, bypassing the manager (and therefore without touching the cache).
    /// </summary>
    public async Task SeedAsync(params Translation[] translations)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        db.Translations.AddRange(translations);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Changes a value directly in the database, bypassing the manager (and therefore without touching the cache).
    /// </summary>
    public async Task UpdateValueDirectlyAsync(string resource, string key, string culture, string value)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        var row = await db.Translations.AsTracking()
            .SingleAsync(t => t.Resource == resource && t.Key == key && t.Culture == culture, TestContext.Current.CancellationToken);

        row.Value = value;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads all the rows currently stored in the database.
    /// </summary>
    public async Task<List<Translation>> GetRowsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        return await db.Translations.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Runs an action with a <see cref="ILocalizationManager"/> resolved from its own DI scope, like a web request would.
    /// </summary>
    public async Task WithManagerAsync(Func<ILocalizationManager, Task> action)
    {
        using var scope = Services.CreateScope();

        await action(scope.ServiceProvider.GetRequiredService<ILocalizationManager>());
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();

        TestData.DeleteDatabase(databasePath);
    }
}

/// <summary>
/// Runs an action right before the next SaveChanges, only once.
/// </summary>
internal sealed class SaveHook : SaveChangesInterceptor
{
    private Func<Task>? beforeNextSave;

    public Func<Task>? BeforeNextSave
    {
        set => Volatile.Write(ref beforeNextSave, value);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var action = Interlocked.Exchange(ref beforeNextSave, null);

        if (action != null)
        {
            await action();
        }

        return result;
    }
}

/// <summary>
/// Counts the SELECT commands executed by Entity Framework Core.
/// </summary>
internal sealed class QueryCounter : DbCommandInterceptor
{
    private int count;

    public int Count => Volatile.Read(ref count);

    public void Reset() => Interlocked.Exchange(ref count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref count);

        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);

        return ValueTask.FromResult(result);
    }
}
