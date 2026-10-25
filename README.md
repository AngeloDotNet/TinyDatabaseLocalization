<p align="center">
  <img src="https://raw.githubusercontent.com/AngeloDotNet/TinyDatabaseLocalization/main/LibraryTools.png" alt="TinyLocalization logo" width="120" />
</p>

<h1 align="center">TinyLocalization</h1>

<p align="center">
  Database-backed localization for .NET: store your translations in SQLite, SQL Server, PostgreSQL or MySQL and read them through the standard <code>IStringLocalizer</code>, with caching built in.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/TinyLocalization"><img alt="NuGet" src="https://img.shields.io/nuget/v/TinyLocalization?style=flat-square&logo=nuget" /></a>
  <a href="https://www.nuget.org/packages/TinyLocalization"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/TinyLocalization?style=flat-square&logo=nuget" /></a>
  <a href="https://github.com/AngeloDotNet/TinyDatabaseLocalization/actions/workflows/publish.yml"><img alt="Publish" src="https://img.shields.io/github/actions/workflow/status/AngeloDotNet/TinyDatabaseLocalization/publish.yml?style=flat-square&label=publish" /></a>
  <img alt=".NET 8 | 9 | 10" src="https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?style=flat-square&logo=dotnet" />
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-green?style=flat-square" /></a>
</p>

<p align="center">
  <a href="#overview">Overview</a> •
  <a href="#features">Features</a> •
  <a href="#getting-started">Getting started</a> •
  <a href="#configuration">Configuration</a> •
  <a href="#managing-translations">Managing translations</a> •
  <a href="#how-it-works">How it works</a> •
  <a href="#project-structure">Project structure</a> •
  <a href="#contributing">Contributing</a> •
  <a href="#license">License</a>
</p>

---

## Overview

TinyLocalization lets an application keep its translations in a database instead of `.resx` files, so they can be added, corrected and removed at runtime without a new deployment.

- Plugs into the standard `Microsoft.Extensions.Localization` abstractions: `IStringLocalizer`, `IStringLocalizer<T>` and `IStringLocalizerFactory`.
- Resolves the culture the usual way, with fallback to parent cultures, a global fallback culture and the invariant culture.
- Keeps lookups fast with [FusionCache](https://github.com/ZiggyCreatures/FusionCache): in-memory by default, with optional Redis as a second level and as a backplane for multi-instance deployments.
- Ships a small `ILocalizationManager` service to add, update, remove and invalidate translations from your own admin screens or APIs.

It targets **.NET 8, .NET 9 and .NET 10** and is built on Entity Framework Core.

## Features

### Localization

- **Standard API**: inject `IStringLocalizer<T>` or create localizers from `IStringLocalizerFactory`, exactly as you would with resource files.
- **Culture fallback**: current culture, then its parents (`it-IT` → `it`), then an optional global fallback culture, then the invariant culture.
- **Format arguments**: `localizer["Welcome", name]` works as expected. A stored text with a malformed format string never throws into the page: it is returned as is.
- **Missing keys**: return the key itself (default) or an empty string, and the `ResourceNotFound` flag is set correctly in both cases.
- **`WithCulture`**: get a localizer bound to a specific culture without changing `CultureInfo.CurrentUICulture`.

### Performance and caching

- **One query per resource and culture**: the translations of a resource for a culture are loaded together and cached as a single snapshot, instead of one query per key.
- **Negative caching**: a culture with no translations is cached too, so missing cultures do not hit the database on every call.
- **FusionCache**: fail-safe, factory timeouts, eager refresh and protection against cache stampede are available through the options.
- **Distributed setup**: optional Redis distributed cache and Redis backplane, so invalidations reach every instance.

### Data and integration

- **Four database providers**: SQLite, SQL Server, PostgreSQL and MySQL.
- **Simple model**: a single `Translations` table, unique on `(Resource, Key, Culture)`.
- **Safe writes**: concurrent inserts of the same translation are resolved as an update instead of failing.
- **Cache invalidation hook**: implement `ICacheInvalidationPublisher` to notify other systems when translations change.

## Getting started

### Prerequisites

- .NET 8, 9 or 10 SDK
- A supported database (SQLite is the easiest way to try the library)
- Redis, only if you want a distributed cache or a backplane (optional)

### Install

```bash
dotnet add package TinyLocalization
```

### Register the services

```csharp
using TinyLocalization.Data;
using TinyLocalization.Enums;
using TinyLocalization.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. The database that stores the translations
builder.Services.AddLocalizationDbContext<LocalizationDbContext>(options =>
{
    options.DatabaseType = DatabaseType.SQLite;
    options.ConnectionString = "Data Source=localization.db";
});

// 2. The cache (in memory). See "Distributed cache" below for Redis.
builder.Services.AddDefaultFusionCache();

// 3. The localizer, the factory and the manager
builder.Services.AddDatabaseLocalization(options =>
{
    options.GlobalFallbackCulture = "en";
});

var app = builder.Build();

// Pick the request culture as you normally would in ASP.NET Core
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "it")
    .AddSupportedUICultures("en", "it"));

app.Run();
```

### Create the database schema

The library does not ship migrations. For a quick start create the schema at startup:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();
    db.Database.EnsureCreated();
}
```

For production, generate migrations in **your** project and tell the library where they live with the `MigrationsAssembly` option (see [Database options](#database-options)).

### Use the localizer

```csharp
public class HomeController(IStringLocalizer<HomeController> localizer) : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = localizer["Welcome"];
        ViewData["Greeting"] = localizer["Hello {0}", User.Identity?.Name ?? "guest"];

        return View();
    }
}
```

The resource name for `IStringLocalizer<T>` is the **full name of `T`** (here `MyApp.Controllers.HomeController`). A localizer created with `factory.Create("Shared", "MyApp")` uses the resource `MyApp.Shared`.

### Distributed cache

To share the cache between instances, use the Redis-based registration instead of `AddDefaultFusionCache`:

```csharp
builder.Services.AddDistributedFusionCache(options =>
{
    options.RedisCacheConnectionString = "localhost:6379";
    options.RedisBackplaneConnectionString = "localhost:6379";
});
```

`AddDistributedLogsFusionCache` does the same and additionally lets you choose the log level of FusionCache events.

## Configuration

### Localization options

Configured in `AddDatabaseLocalization`.

| Option | Default | Description |
| --- | --- | --- |
| `CacheDuration` | `10 minutes` | How long a cached snapshot stays valid. |
| `FallbackToParentCultures` | `true` | Try the parent cultures (`it-IT` → `it`) when a translation is missing. |
| `GlobalFallbackCulture` | `null` | Culture used when nothing was found for the current culture and its parents. |
| `CacheKeyPrefix` | `"db_localize"` | Prefix of the cache keys. Change it when several applications share the same cache. |
| `ReturnKeyIfNotFound` | `true` | Return the key when a translation is missing. When `false`, an empty string is returned. |

### Database options

Configured in `AddLocalizationDbContext`.

| Option | Default | Description |
| --- | --- | --- |
| `DatabaseType` | `None` | `SQLite`, `SqlServer`, `PostgreSQL` or `MySQL`. Required. |
| `ConnectionString` | empty | Connection string of the database. Required. |
| `MigrationsAssembly` | empty | Assembly that contains your migrations. When empty, the assembly of the context is used. |
| `MigrationsHistoryTable` | `__EFMigrationsHistory` | Name of the migrations history table. |
| `SQLServerCompatibilityLevel` | `150` | Compatibility level used with SQL Server. |
| `QueryTrackingBehavior` | `NoTracking` | Tracking behavior applied to queries. |
| `EnableUseQueryTrackingBehavior` | `false` | When `true`, full tracking is used and `QueryTrackingBehavior` is ignored. |
| `LogCommandsToConsole` | `false` | Writes every executed SQL command to the console. Meant for local debugging. |
| `EnableSensitiveDataLogging` | `false` | Includes parameter values in EF Core logs. Never enable it in production. |
| `EnableDetailedErrors` | `false` | Enables detailed EF Core error messages. |

### Cache options

`AddDefaultFusionCache`, `AddDistributedFusionCache` and `AddDistributedLogsFusionCache` expose their own option classes. The most relevant settings are:

| Option | Default | Description |
| --- | --- | --- |
| `CacheDurationInMinutes` | `10` | Default lifetime of a cache entry. |
| `FailSafeModeEnabled` | `true` | Serve a stale value when the database is temporarily unavailable. |
| `FailSafeMaxDurationInMinutes` | `120` | How long a stale value can still be served. |
| `FailSafeThrottleDurationInSeconds` | `30` | Pause before retrying after a failure. |
| `FactorySoftTimeoutInMilliseconds` / `FactoryHardTimeoutInMilliseconds` | `100` / `1500` | Timeouts of the database lookup. |
| `DistributedCacheSoftTimeoutInMilliseconds` / `DistributedCacheHardTimeoutInMilliseconds` | `1000` / `2000` | Timeouts of a single Redis operation (distributed registrations). |
| `RedisCacheConnectionString` / `RedisBackplaneConnectionString` | empty | Redis endpoints (distributed registrations). |

## Managing translations

Inject `ILocalizationManager` to change translations at runtime. Every operation updates the database and invalidates the cache.

```csharp
public class TranslationsService(ILocalizationManager manager)
{
    public Task SaveAsync(CancellationToken ct) =>
        manager.AddOrUpdateAsync(new Translation
        {
            Resource = "MyApp.Controllers.HomeController",
            Key = "Welcome",
            Culture = "it",
            Value = "Benvenuto"
        }, ct);

    public Task<bool> DeleteAsync(CancellationToken ct) =>
        manager.RemoveAsync("MyApp.Controllers.HomeController", "Welcome", "it", ct);

    // After editing the database directly (scripts, imports, other tools)
    public Task RefreshAsync(CancellationToken ct) =>
        manager.InvalidateResourceAsync("MyApp.Controllers.HomeController", ct);
}
```

- Use an empty `Culture` (`""`) for the invariant culture.
- `AddOrUpdateAsync` creates the row when it does not exist and updates its value otherwise.
- Changes made **directly** on the database are not seen until the cache entry expires or `InvalidateResourceAsync` is called.

## How it works

```
 IStringLocalizer<T>
        │  localizer["Key"]
        ▼
 EfStringLocalizer ──► culture chain: it-IT → it → global fallback → invariant
        │
        │  for each culture: snapshot of (resource, culture)
        ▼
   FusionCache ──── hit ──► value
        │ miss
        ▼
 LocalizationDbContext ──► one query: all keys of the resource for that culture
```

1. The localizer builds the culture chain for the current UI culture.
2. For each culture it asks the cache for the snapshot of that `(resource, culture)`.
3. On a miss, a single query loads every translation of that resource and culture, and the result is cached.
4. The first culture of the chain that contains the key wins.

## Project structure

```
src/
└── TinyLocalization/
    ├── Data/            LocalizationDbContext
    ├── Entities/        Translation
    ├── Enums/           DatabaseType
    ├── Extensions/      Service registration (AddDatabaseLocalization, AddLocalizationDbContext, ...)
    ├── Localization/    EfStringLocalizer, factory and cache keys
    ├── Options/         Localization, database and FusionCache options
    └── Services/        LocalizationManager and cache invalidation contracts
tests/
└── TinyLocalization.IntegrationTests/   xUnit v3 integration tests (real SQLite + FusionCache)
```

## Running the tests

```bash
dotnet test
```

The integration tests use a real SQLite database stored in a temporary file and a real FusionCache instance, so they exercise the same code paths as an application.

## Contributing

The project is constantly evolving and contributions are always welcome. Feel free to report issues and submit pull requests:

1. Fork the repository
2. Create a feature branch (starting from the `develop` branch)
3. Make your changes and add or update the tests
4. Submit a pull request (targeting `develop`)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Special thanks

- [@jodydonetti](https://github.com/jodydonetti), author of FusionCache

## Support the project

If TinyLocalization is useful to you, give it a ⭐ on GitHub to help others discover it, or [buy me a coffee](https://www.buymeacoffee.com/angelodotnet).
