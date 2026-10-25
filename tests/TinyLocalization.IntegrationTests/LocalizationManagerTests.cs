using Microsoft.Extensions.DependencyInjection;
using TinyLocalization.IntegrationTests.Infrastructure;
using TinyLocalization.Services.Interfaces;
using static TinyLocalization.IntegrationTests.Infrastructure.TestData;

namespace TinyLocalization.IntegrationTests;

public class LocalizationManagerTests
{
    [Fact]
    public async Task Adds_a_new_translation()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Ciao"), cancellationToken));

        var row = Assert.Single(await host.GetRowsAsync());

        Assert.Equal(("Shared", "Hello", "it", "Ciao"), (row.Resource, row.Key, row.Culture, row.Value));
    }

    [Fact]
    public async Task Updates_an_existing_translation_without_creating_a_duplicate()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Ciao"), cancellationToken));
        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Salve"), cancellationToken));

        var row = Assert.Single(await host.GetRowsAsync());

        Assert.Equal("Salve", row.Value);
    }

    [Fact]
    public async Task Makes_an_updated_translation_visible_to_the_localizer_immediately()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.SeedAsync(Tr("Shared", "Hello", "it", "Ciao"));
        var localizer = host.Localizer();

        using var culture = new CultureScope("it");

        // the first read caches the snapshot
        Assert.Equal("Ciao", localizer["Hello"].Value);

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Salve"), cancellationToken));

        Assert.Equal("Salve", localizer["Hello"].Value);
    }

    [Fact]
    public async Task Makes_a_new_translation_visible_even_if_the_culture_was_cached_as_empty()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        var localizer = host.Localizer();

        using var culture = new CultureScope("it");

        // nothing is stored yet: the empty snapshot of "it" is cached
        Assert.True(localizer["Hello"].ResourceNotFound);

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Ciao"), cancellationToken));

        Assert.Equal("Ciao", localizer["Hello"].Value);
    }

    [Fact]
    public async Task Removes_a_translation_and_invalidates_the_cache()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.SeedAsync(Tr("Shared", "Hello", "it", "Ciao"));
        var localizer = host.Localizer();

        using var culture = new CultureScope("it");

        Assert.Equal("Ciao", localizer["Hello"].Value);

        var removed = false;
        await host.WithManagerAsync(async m => removed = await m.RemoveAsync("Shared", "Hello", "it", cancellationToken));

        Assert.True(removed);
        Assert.Empty(await host.GetRowsAsync());
        Assert.True(localizer["Hello"].ResourceNotFound);
    }

    [Fact]
    public async Task Returns_false_when_removing_a_translation_that_does_not_exist()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;

        var removed = true;
        await host.WithManagerAsync(async m => removed = await m.RemoveAsync("Shared", "Missing", "it", cancellationToken));

        Assert.False(removed);
    }

    [Fact]
    public async Task Rejects_a_null_translation()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(()
            => host.WithManagerAsync(m => m.AddOrUpdateAsync(null!, cancellationToken)));
    }

    [Fact]
    public async Task Serves_changes_made_directly_on_the_database_from_the_cache_until_the_resource_is_invalidated()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.SeedAsync(Tr("Shared", "Hello", "it", "Ciao"));
        var localizer = host.Localizer();

        using var culture = new CultureScope("it");

        Assert.Equal("Ciao", localizer["Hello"].Value);

        await host.UpdateValueDirectlyAsync("Shared", "Hello", "it", "Salve");

        Assert.Equal("Ciao", localizer["Hello"].Value);

        await host.WithManagerAsync(m => m.InvalidateResourceAsync("Shared", cancellationToken));

        Assert.Equal("Salve", localizer["Hello"].Value);
    }

    [Fact]
    public async Task Invalidating_a_resource_does_not_affect_other_resources()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.SeedAsync(Tr("Orders", "Title", "it", "Ordini"), Tr("Customers", "Title", "it", "Clienti"));

        using var culture = new CultureScope("it");

        Assert.Equal("Ordini", host.Localizer("Orders")["Title"].Value);
        Assert.Equal("Clienti", host.Localizer("Customers")["Title"].Value);

        await host.UpdateValueDirectlyAsync("Orders", "Title", "it", "Ordini 2");
        await host.UpdateValueDirectlyAsync("Customers", "Title", "it", "Clienti 2");
        await host.WithManagerAsync(m => m.InvalidateResourceAsync("Orders", cancellationToken));

        Assert.Equal("Ordini 2", host.Localizer("Orders")["Title"].Value);
        Assert.Equal("Clienti", host.Localizer("Customers")["Title"].Value);
    }

    [Fact]
    public async Task Applies_the_value_as_an_update_when_another_writer_inserts_the_same_translation_first()
    {
        await using var host = new LocalizationTestHost();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Reproduces the race deterministically: after the manager has found nothing and right before it inserts,
        // another writer stores the same (resource, key, culture), so the insert hits the unique index.
        host.SaveHook.BeforeNextSave = () => host.SeedAsync(Tr("Shared", "Race", "en", "From the other writer"));

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Race", "en", "From the manager"), cancellationToken));

        var row = Assert.Single(await host.GetRowsAsync());

        Assert.Equal("From the manager", row.Value);
    }

    [Fact]
    public async Task Publishes_a_single_invalidation_for_every_change()
    {
        var publisher = new RecordingPublisher();
        await using var host = new LocalizationTestHost(configureServices: s => s.AddSingleton<ICacheInvalidationPublisher>(publisher));
        var cancellationToken = TestContext.Current.CancellationToken;

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Ciao"), cancellationToken));
        await host.WithManagerAsync(m => m.RemoveAsync("Shared", "Hello", "it", cancellationToken));

        Assert.Equal(2, publisher.Singles.Count);
        Assert.All(publisher.Singles, p => Assert.Equal(new PublishedSingle("Shared", "Hello", "it"), p));
    }

    [Fact]
    public async Task Publishes_the_keys_of_the_resource_when_invalidating_it()
    {
        var publisher = new RecordingPublisher();
        await using var host = new LocalizationTestHost(configureServices: s => s.AddSingleton<ICacheInvalidationPublisher>(publisher));
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.SeedAsync(
            Tr("Shared", "Hello", "it", "Ciao"), Tr("Shared", "Hello", "en", "Hello"), Tr("Shared", "Bye", "it", "Arrivederci"),
            Tr("Other", "Title", "it", "Titolo"));

        await host.WithManagerAsync(m => m.InvalidateResourceAsync("Shared", cancellationToken));

        var published = Assert.Single(publisher.Resources);

        Assert.Equal("Shared", published.Resource);
        Assert.Equal(3, published.Keys.Count);
        Assert.DoesNotContain(published.Keys, k => k.Key == "Title");
    }

    [Fact]
    public async Task Does_not_fail_the_write_when_the_publisher_fails()
    {
        var publisher = new RecordingPublisher { ThrowOnPublish = true };
        await using var host = new LocalizationTestHost(configureServices: s => s.AddSingleton<ICacheInvalidationPublisher>(publisher));
        var cancellationToken = TestContext.Current.CancellationToken;

        await host.WithManagerAsync(m => m.AddOrUpdateAsync(Tr("Shared", "Hello", "it", "Ciao"), cancellationToken));

        Assert.Single(await host.GetRowsAsync());
    }
}
