using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;
using TinyLocalization.Enums;
using TinyLocalization.Extensions;
using TinyLocalization.IntegrationTests.Infrastructure;
using ZiggyCreatures.Caching.Fusion;
using static TinyLocalization.IntegrationTests.Infrastructure.TestData;

namespace TinyLocalization.IntegrationTests;

public class ServiceRegistrationTests
{
    [Fact]
    public void Rejects_a_missing_database_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(()
            => services.AddLocalizationDbContext<LocalizationDbContext>(o => o.ConnectionString = "Data Source=localization.db"));
    }

    [Fact]
    public void Rejects_a_missing_connection_string()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(()
            => services.AddLocalizationDbContext<LocalizationDbContext>(o => o.DatabaseType = DatabaseType.SQLite));
    }

    [Fact]
    public async Task Works_end_to_end_with_the_library_registration_extensions()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"tinylocalization-registration-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFusionCache();
        services.AddLocalizationDbContext<LocalizationDbContext>(o =>
        {
            o.DatabaseType = DatabaseType.SQLite;
            o.ConnectionString = $"Data Source={databasePath}";
        });
        services.AddDatabaseLocalization(o => o.GlobalFallbackCulture = "en");

        await using var provider = services.BuildServiceProvider();

        try
        {
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

                await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                db.Translations.Add(Tr("Shared", "Hello", "en", "Hello"));
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var localizer = provider.GetRequiredService<IStringLocalizerFactory>().Create("Shared", string.Empty);

            using var culture = new CultureScope("fr-FR");

            Assert.Equal("Hello", localizer["Hello"].Value);
        }
        finally
        {
            TestData.DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Uses_the_context_assembly_for_migrations_when_none_is_specified()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"tinylocalization-migrations-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalizationDbContext<LocalizationDbContext>(o =>
        {
            o.DatabaseType = DatabaseType.SQLite;
            o.ConnectionString = $"Data Source={databasePath}";
        });

        await using var provider = services.BuildServiceProvider();

        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

            var migrationsAssembly = db.GetService<IMigrationsAssembly>();

            Assert.Equal(typeof(LocalizationDbContext).Assembly, migrationsAssembly.Assembly);
        }
        finally
        {
            TestData.DeleteDatabase(databasePath);
        }
    }
}
