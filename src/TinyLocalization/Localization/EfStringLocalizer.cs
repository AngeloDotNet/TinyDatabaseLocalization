using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;
using TinyLocalization.Options;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Localization;

/// <summary>
/// Provides a string localizer that retrieves localized strings from a database, supporting culture-specific lookups,
/// parent culture fallback, and caching for efficient localization in .NET applications.
/// </summary>
/// <remarks>EfStringLocalizer implements IStringLocalizer and is designed for scenarios where localized resources
/// are stored in a database rather than static files. It supports fallback to parent cultures and a global fallback
/// culture. All the translations of a resource for a given culture are loaded with a single query and cached as one
/// snapshot, so a lookup never hits the database once the snapshot is cached and warming up a resource costs one
/// query per culture instead of one query per key.</remarks>
/// <param name="serviceProvider">The service provider used to resolve dependencies and create scopes for database access during localization
/// operations.</param>
/// <param name="resource">The name of the resource for which localized strings are managed. This typically corresponds to a logical grouping
/// or source of localized values.</param>
/// <param name="fusionCache">The cache instance used to store and retrieve localized strings, reducing database queries and improving
/// performance.</param>
/// <param name="options">The localization options that configure fallback behavior, cache settings, and other localization features.</param>
public class EfStringLocalizer(IServiceProvider serviceProvider, string resource, IFusionCache fusionCache, DbLocalizationOptions options) : IStringLocalizer
{
    /// <summary>
    /// Culture fallback chains already computed. The chain only depends on the culture name and on the fallback
    /// settings, so it is computed once and reused by every lookup.
    /// </summary>
    private static readonly ConcurrentDictionary<(string Culture, bool Parents, string? GlobalFallback, bool Invariant), string[]> CultureChains = new();

    /// <summary>
    /// Returned when a snapshot cannot be retrieved, so callers never have to deal with null.
    /// </summary>
    private static readonly Dictionary<string, string> EmptySnapshot = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores the resource string, defaulting to an empty string if the provided value is null.
    /// </summary>
    private readonly string resource = resource ?? string.Empty;

    /// <summary>
    /// Cache keys already built for this resource, indexed by culture name, to avoid building the same string on
    /// every lookup.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> cacheKeys = new();

    /// <summary>
    /// The culture requested through <see cref="WithCulture(CultureInfo)"/>, or null to use the current UI culture.
    /// </summary>
    private CultureInfo? cultureOverride;

    private CultureInfo CurrentCulture => cultureOverride ?? CultureInfo.CurrentUICulture;

    /// <summary>
    /// Builds the ordered list of culture names to try for a culture: the culture itself, its parents (when enabled),
    /// the global fallback culture and, optionally, the invariant culture.
    /// </summary>
    private static string[] BuildCultureChain(CultureInfo culture, bool includeParents, string? globalFallback, bool includeInvariant)
    {
        var chain = new List<string>();

        if (!string.IsNullOrEmpty(culture.Name))
        {
            chain.Add(culture.Name);
        }

        if (includeParents)
        {
            var parent = culture.Parent;

            while (parent != null && !string.IsNullOrEmpty(parent.Name))
            {
                if (!chain.Contains(parent.Name))
                {
                    chain.Add(parent.Name);
                }

                parent = parent.Parent;
            }
        }

        if (!string.IsNullOrEmpty(globalFallback) && !chain.Contains(globalFallback))
        {
            chain.Add(globalFallback);
        }

        if (includeInvariant && !chain.Contains(string.Empty))
        {
            chain.Add(string.Empty);
        }

        return chain.ToArray();
    }

    /// <summary>
    /// Gets the cached culture chain for the specified culture.
    /// </summary>
    private string[] GetCultureChain(CultureInfo culture)
    {
        var key = (culture.Name, options.FallbackToParentCultures, options.GlobalFallbackCulture, true);

        return CultureChains.GetOrAdd(key,
            static (k, c) => BuildCultureChain(c, k.Parents, k.GlobalFallback, k.Invariant), culture);
    }

    /// <summary>
    /// Gets the cache key of the snapshot for the specified culture.
    /// </summary>
    private string GetCacheKey(string cultureName)
        => cacheKeys.GetOrAdd(cultureName,
            static (c, s) => LocalizationCacheKeys.ForSnapshot(s.Prefix, s.Resource, c),
            (Prefix: options.CacheKeyPrefix, Resource: resource));

    /// <summary>
    /// Loads from the database all the translations of the resource for the specified culture, with a single query.
    /// </summary>
    /// <param name="cultureName">The culture name. An empty string identifies the invariant culture.</param>
    /// <returns>A dictionary that maps each translation key to its value.</returns>
    private Dictionary<string, string> LoadSnapshotFromDb(string cultureName)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        return db.Translations
            .AsNoTracking()
            .Where(t => t.Resource == resource && t.Culture == cultureName)
            .Select(t => new { t.Key, t.Value })
            .AsEnumerable()
            .ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the snapshot of all the translations of the resource for the specified culture, loading it from the
    /// database only when it is not already cached. A culture without translations is cached as an empty snapshot,
    /// which acts as a negative cache entry for the whole culture.
    /// </summary>
    private Dictionary<string, string> GetSnapshot(string cultureName)
    {
        var snapshot = fusionCache.GetOrSet<Dictionary<string, string>>(GetCacheKey(cultureName), (ctx, ct) =>
        {
            ctx.Options.Duration = options.CacheDuration;

            return LoadSnapshotFromDb(cultureName);
        });

        return snapshot ?? EmptySnapshot;
    }

    /// <summary>
    /// Retrieves a localized string for the specified resource name, attempting to resolve it through the current UI
    /// culture and configured fallback cultures.
    /// </summary>
    /// <remarks>The method first attempts to find the resource string in the current UI culture. If not
    /// found, it falls back to parent cultures, a global fallback culture if configured, and finally the invariant
    /// culture. Each culture is served from a cached snapshot. The method does not throw if the resource is
    /// not found; instead, it returns null.</remarks>
    /// <param name="name">The name of the resource string to retrieve. Cannot be null.</param>
    /// <returns>The localized string associated with the specified name, or null if the string is not found in any of the
    /// attempted cultures.</returns>
    private string? GetStringWithFallback(string name)
    {
        foreach (var cultureName in GetCultureChain(CurrentCulture))
        {
            if (GetSnapshot(cultureName).TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the localized string associated with the specified name, returning a fallback value if the resource is not
    /// found.
    /// </summary>
    /// <remarks>If the requested string is not found and the option to return the key is enabled, the key
    /// will be returned as the value. Otherwise, an empty string is returned.</remarks>
    /// <param name="name">The name of the string to retrieve from the localization resources.</param>
    /// <returns>A <see cref="LocalizedString"/> object containing the localized value. If the resource is not found, the
    /// returned object contains either the key itself or an empty string, depending on configuration.</returns>
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetStringWithFallback(name);

            if (value != null)
            {
                return new LocalizedString(name, value, resourceNotFound: false);
            }

            if (options.ReturnKeyIfNotFound)
            {
                return new LocalizedString(name, name, resourceNotFound: true);
            }

            return new LocalizedString(name, string.Empty, resourceNotFound: true);
        }
    }

    /// <summary>
    /// Gets the localized string associated with the specified key, formatted with the provided arguments.
    /// </summary>
    /// <remarks>If the specified key is not found and the option to return the key is disabled, an empty
    /// string is returned. If the stored text is not a valid composite format string, the unformatted text is returned
    /// instead of throwing a <see cref="FormatException"/> into the calling page.</remarks>
    /// <param name="name">The key that identifies the localized string to retrieve.</param>
    /// <param name="arguments">An array of objects to format the localized string with. Each object is used to replace a corresponding
    /// placeholder in the localized string.</param>
    /// <returns>A LocalizedString object containing the key, the formatted localized value, and a flag indicating whether the
    /// string was found.</returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = GetStringWithFallback(name);
            var notFound = found is null;
            var format = found ?? (options.ReturnKeyIfNotFound ? name : string.Empty);

            string value;

            try
            {
                value = string.Format(CultureInfo.CurrentCulture, format, arguments);
            }
            catch (FormatException)
            {
                value = format;
            }

            return new LocalizedString(name, value, notFound);
        }
    }

    /// <summary>
    /// Retrieves all localized strings for the current user interface culture, with an option to include strings from
    /// parent cultures and a global fallback culture if configured.
    /// </summary>
    /// <remarks>If includeParentCultures is set to true and fallback to parent cultures is enabled, the
    /// method searches through the hierarchy of parent cultures in order of specificity. If a global fallback culture
    /// is configured and not already included, it is also searched. The returned collection contains only the first
    /// occurrence of each key, prioritizing more specific cultures. The data comes from the cached snapshots, so it
    /// is as fresh as the values returned by the indexers.</remarks>
    /// <param name="includeParentCultures">true to include localized strings from parent cultures in addition to the current culture; otherwise, false.</param>
    /// <returns>A collection of LocalizedString objects representing the localized strings available for the specified cultures.
    /// Each key appears only once, with values from the highest-priority culture.</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var currentCulture = CurrentCulture;

        // For GetAllStrings we'll try current culture and, if requested, parents
        var cultures = new List<string> { currentCulture.Name };

        if (includeParentCultures && options.FallbackToParentCultures)
        {
            var parent = currentCulture.Parent;

            while (parent != null && !string.IsNullOrEmpty(parent.Name))
            {
                cultures.Add(parent.Name);
                parent = parent.Parent;
            }
        }

        // Also include global fallback if configured
        if (!string.IsNullOrEmpty(options.GlobalFallbackCulture) && !cultures.Contains(options.GlobalFallbackCulture))
        {
            cultures.Add(options.GlobalFallbackCulture!);
        }

        var results = new Dictionary<string, LocalizedString>(StringComparer.Ordinal);

        foreach (var cultureName in cultures)
        {
            foreach (var (key, value) in GetSnapshot(cultureName))
            {
                // keep first occurrence (higher priority culture first)
                if (!results.ContainsKey(key))
                {
                    results[key] = new LocalizedString(key, value, resourceNotFound: false);
                }
            }
        }

        return results.Values.ToList();
    }

    /// <summary>
    /// Returns a localizer that uses the specified culture for resource lookups instead of the current UI culture.
    /// </summary>
    /// <remarks>The returned localizer shares the cache with this instance and does not change
    /// <see cref="CultureInfo.CurrentUICulture"/>.</remarks>
    /// <param name="culture">The culture to use for localizing resources.</param>
    /// <returns>A new <see cref="IStringLocalizer"/> bound to the specified culture.</returns>
    public IStringLocalizer WithCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return new EfStringLocalizer(serviceProvider, resource, fusionCache, options) { cultureOverride = culture };
    }
}
