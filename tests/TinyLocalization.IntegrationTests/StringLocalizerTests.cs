using System.Globalization;
using TinyLocalization.IntegrationTests.Infrastructure;
using TinyLocalization.Localization;
using static TinyLocalization.IntegrationTests.Infrastructure.TestData;

namespace TinyLocalization.IntegrationTests;

public class StringLocalizerTests
{
    [Fact]
    public async Task Returns_the_translation_for_the_current_culture()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Hello", "en", "Hello"), Tr("Shared", "Hello", "it", "Ciao"));
        var localizer = host.Localizer();

        using (new CultureScope("it"))
        {
            var result = localizer["Hello"];

            Assert.Equal("Ciao", result.Value);
            Assert.False(result.ResourceNotFound);
        }

        using (new CultureScope("en"))
        {
            Assert.Equal("Hello", localizer["Hello"].Value);
        }
    }

    [Fact]
    public async Task Prefers_the_most_specific_culture()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Color", "en", "Color"), Tr("Shared", "Color", "en-GB", "Colour"));
        var localizer = host.Localizer();

        using (new CultureScope("en-GB"))
        {
            Assert.Equal("Colour", localizer["Color"].Value);
        }

        using (new CultureScope("en-US"))
        {
            Assert.Equal("Color", localizer["Color"].Value);
        }
    }

    [Fact]
    public async Task Falls_back_to_the_parent_culture()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Title", "it", "Titolo"));

        using var culture = new CultureScope("it-IT");

        Assert.Equal("Titolo", host.Localizer()["Title"].Value);
    }

    [Fact]
    public async Task Does_not_use_parent_cultures_when_the_fallback_is_disabled()
    {
        await using var host = new LocalizationTestHost(o => o.FallbackToParentCultures = false);
        await host.SeedAsync(Tr("Shared", "Title", "it", "Titolo"));

        using var culture = new CultureScope("it-IT");
        var result = host.Localizer()["Title"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("Title", result.Value);
    }

    [Fact]
    public async Task Falls_back_to_the_global_fallback_culture_before_the_invariant_one()
    {
        await using var host = new LocalizationTestHost(o => o.GlobalFallbackCulture = "en");
        await host.SeedAsync(Tr("Shared", "Hello", "en", "Hello"), Tr("Shared", "Hello", "", "Invariant hello"));

        using var culture = new CultureScope("fr-FR");

        Assert.Equal("Hello", host.Localizer()["Hello"].Value);
    }

    [Fact]
    public async Task Falls_back_to_the_invariant_culture_as_a_last_resort()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Hello", "", "Default hello"));

        using var culture = new CultureScope("de-DE");
        var result = host.Localizer()["Hello"];

        Assert.Equal("Default hello", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Returns_the_key_when_the_translation_is_missing()
    {
        await using var host = new LocalizationTestHost();

        using var culture = new CultureScope("it");
        var result = host.Localizer()["Unknown.Key"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("Unknown.Key", result.Value);
    }

    [Fact]
    public async Task Returns_an_empty_string_when_the_translation_is_missing_and_returning_the_key_is_disabled()
    {
        await using var host = new LocalizationTestHost(o => o.ReturnKeyIfNotFound = false);

        using var culture = new CultureScope("it");
        var result = host.Localizer()["Unknown.Key"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal(string.Empty, result.Value);
    }

    [Fact]
    public async Task Formats_the_translation_with_the_arguments()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Welcome", "", "Welcome {0}!"));

        using var culture = new CultureScope("it");
        var result = host.Localizer()["Welcome", "Mario"];

        Assert.Equal("Welcome Mario!", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Does_not_throw_when_the_stored_text_is_not_a_valid_format_string()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Broken", "", "Broken {0"));

        using var culture = new CultureScope("it");
        var result = host.Localizer()["Broken", "x"];

        Assert.Equal("Broken {0", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public async Task Reports_a_missing_translation_even_when_arguments_are_used()
    {
        await using var host = new LocalizationTestHost();

        using var culture = new CultureScope("it");
        var result = host.Localizer()["Missing {0}", 42];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("Missing 42", result.Value);
    }

    [Fact]
    public async Task Keeps_resources_separate()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Orders", "Title", "en", "Orders"), Tr("Customers", "Title", "en", "Customers"));

        using var culture = new CultureScope("en");

        Assert.Equal("Orders", host.Localizer("Orders")["Title"].Value);
        Assert.Equal("Customers", host.Localizer("Customers")["Title"].Value);
    }

    [Fact]
    public async Task Loads_all_the_keys_of_a_resource_with_a_single_query()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Enumerable.Range(0, 50).Select(i => Tr("Shared", $"Key{i}", "it", $"Valore{i}")).ToArray());
        var localizer = host.Localizer();

        host.Queries.Reset();

        using var culture = new CultureScope("it");

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal($"Valore{i}", localizer[$"Key{i}"].Value);
        }

        Assert.Equal(1, host.Queries.Count);

        // once the snapshot is cached no further query is needed
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal($"Valore{i}", localizer[$"Key{i}"].Value);
        }

        Assert.Equal(1, host.Queries.Count);
    }

    [Fact]
    public async Task Caches_a_culture_without_translations_as_a_whole()
    {
        await using var host = new LocalizationTestHost();
        var localizer = host.Localizer();

        host.Queries.Reset();

        using var culture = new CultureScope("fr");

        for (var i = 0; i < 20; i++)
        {
            Assert.True(localizer[$"Missing{i}"].ResourceNotFound);
        }

        // one query for "fr" and one for the invariant culture, not one per key
        Assert.Equal(2, host.Queries.Count);
    }

    [Fact]
    public async Task Gets_all_the_strings_merging_cultures_by_priority()
    {
        await using var host = new LocalizationTestHost(o => o.GlobalFallbackCulture = "en");
        await host.SeedAsync(
            Tr("Shared", "A", "it", "it-A"), Tr("Shared", "B", "it", "it-B"),
            Tr("Shared", "B", "en", "en-B"), Tr("Shared", "C", "en", "en-C"));
        var localizer = host.Localizer();

        using var culture = new CultureScope("it");
        var all = localizer.GetAllStrings(includeParentCultures: true).ToDictionary(s => s.Name, s => s.Value);

        Assert.Equal(3, all.Count);
        Assert.Equal("it-A", all["A"]);
        Assert.Equal("it-B", all["B"]);
        Assert.Equal("en-C", all["C"]);

        // the snapshots are already cached, so listing again does not query the database
        host.Queries.Reset();
        _ = localizer.GetAllStrings(includeParentCultures: true).ToList();

        Assert.Equal(0, host.Queries.Count);
    }

    [Fact]
    public async Task Uses_the_culture_requested_with_WithCulture_without_changing_the_current_one()
    {
        await using var host = new LocalizationTestHost();
        await host.SeedAsync(Tr("Shared", "Hello", "en", "Hello"), Tr("Shared", "Hello", "it", "Ciao"));

        var localizer = (EfStringLocalizer)host.Localizer();
        var italian = localizer.WithCulture(CultureInfo.GetCultureInfo("it"));

        using var culture = new CultureScope("en");

        Assert.Equal("Ciao", italian["Hello"].Value);
        Assert.Equal("Hello", localizer["Hello"].Value);
        Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
    }
}
