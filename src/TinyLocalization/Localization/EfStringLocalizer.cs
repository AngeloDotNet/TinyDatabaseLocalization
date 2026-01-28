using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;
using TinyLocalization.Options;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Localization;

/// <summary>
/// An <see cref="IStringLocalizer"/> implementation that reads translations from a database
/// and uses <see cref="IFusionCache"/> to cache per-culture lookup results.
/// </summary>
/// <param name="serviceProvider">
/// The <see cref="IServiceProvider"/> used to create scoped services (for example to resolve
/// a <see cref="LocalizationDbContext"/> for database queries).
/// </param>
/// <param name="resource">
/// The resource name (logical resource/container) for which this localizer will resolve keys.
/// This value is normalized to an empty string if null.
/// </param>
/// <param name="fusionCache">
/// The <see cref="IFusionCache"/> instance used to cache translation lookups.
/// </param>
/// <param name="options">
/// The <see cref="DbLocalizationOptions"/> instance controlling caching, fallback and key behavior.
/// </param>
public class EfStringLocalizer(IServiceProvider serviceProvider, string resource, IFusionCache fusionCache, DbLocalizationOptions options) : IStringLocalizer
{
    /// <summary>
    /// Normalized resource name for lookups (never null).
    /// </summary>
    private readonly string resource = resource ?? string.Empty;

    /// <summary>
    /// Query the database directly (no caching) for the given resource key and culture.
    /// </summary>
    /// <param name="name">The translation key to look up.</param>
    /// <param name="cultureName">
    /// The culture name to query for. Use an empty string to indicate the invariant culture.
    /// </param>
    /// <returns>
    /// The translation value if found; otherwise <c>null</c> if no entry exists for the specified key and culture.
    /// </returns>
    private string? GetStringFromDb_NoCache(string name, string cultureName)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        var entry = db.Translations.FirstOrDefault(t => t.Resource == resource && t.Key == name && t.Culture == cultureName);

        return entry?.Value;
    }

    /// <summary>
    /// Attempt to resolve the specified key using a culture fallback chain and per-culture caching.
    /// The chain tries the current UI culture, optional parent cultures (if enabled by options),
    /// an optional global fallback culture, and finally the invariant culture.
    /// Each culture lookup is cached individually using <see cref="IFusionCache.GetOrSet{T}"/>.
    /// </summary>
    /// <param name="name">The translation key to resolve.</param>
    /// <returns>
    /// The first non-<c>null</c> translation value found along the fallback chain; otherwise <c>null</c>.
    /// </returns>
    private string? GetStringWithFallback(string name)
    {
        // Determine culture chain to try
        var culture = CultureInfo.CurrentUICulture;
        var culturesToTry = new List<string>();

        if (!string.IsNullOrEmpty(culture.Name))
        {
            culturesToTry.Add(culture.Name);
        }

        if (options.FallbackToParentCultures)
        {
            var parent = culture.Parent;

            while (parent != null && !string.IsNullOrEmpty(parent.Name))
            {
                if (!culturesToTry.Contains(parent.Name))
                {
                    culturesToTry.Add(parent.Name);
                }

                parent = parent.Parent;
            }
        }

        if (!string.IsNullOrEmpty(options.GlobalFallbackCulture) && !culturesToTry.Contains(options.GlobalFallbackCulture))
        {
            culturesToTry.Add(options.GlobalFallbackCulture!);
        }

        // Add invariant as last resort
        if (!culturesToTry.Contains(string.Empty))
        {
            culturesToTry.Add(string.Empty);
        }

        // Try sequentially; each lookup is cached per culture
        foreach (var cultureName in culturesToTry)
        {
            var cacheKey = BuildCacheKey(resource, name, cultureName);
            // Use FusionCache GetOrSet with cache options
            var value = fusionCache.GetOrSet<string?>(cacheKey, (ctx, ct) =>
            {
                ctx.Options.Duration = options.CacheDuration;

                // Do DB lookup for this culture
                var dbValue = GetStringFromDb_NoCache(name, cultureName);

                // if null, store null to indicate not found (we'll treat as not found and continue fallback)
                return dbValue;
            });

            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Build a normalized cache key for the given resource, key and culture.
    /// </summary>
    /// <param name="resource">The resource name used for grouping translations.</param>
    /// <param name="name">The translation key.</param>
    /// <param name="cultureName">The culture name (empty string for invariant culture).</param>
    /// <returns>A string cache key composed of the configured cache prefix and provided segments.</returns>
    private string BuildCacheKey(string resource, string name, string cultureName)
    {
        // Use prefix so cache keys are grouped and easier to remove
        // Note: cultureName can be empty string for invariant; normalize to "_" to avoid empty segments if desired
        var cultureSegment = string.IsNullOrEmpty(cultureName) ? "_" : cultureName;

        return $"{options.CacheKeyPrefix}:{resource}:{name}:{cultureSegment}";
    }

    /// <summary>
    /// Gets the localized string for the specified key using the configured fallback strategy.
    /// If a translation is found, returns a <see cref="LocalizedString"/> with <see cref="LocalizedString.ResourceNotFound"/>
    /// set to <c>false</c>. If not found, behavior depends on <see cref="DbLocalizationOptions.ReturnKeyIfNotFound"/>.
    /// </summary>
    /// <param name="name">The translation key to lookup.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> with the resolved or fallback value. If the key was not found and
    /// <see cref="DbLocalizationOptions.ReturnKeyIfNotFound"/> is <c>true</c>, the key is returned as the value
    /// and <see cref="LocalizedString.ResourceNotFound"/> will be <c>true</c>.
    /// </returns>
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
    /// Gets the localized and formatted string for the specified key, formatting it with the provided arguments.
    /// </summary>
    /// <param name="name">The translation key to lookup.</param>
    /// <param name="arguments">Format arguments used with <see cref="string.Format(string, object[])"/> against the found format.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> containing the formatted value. If the format is not found and
    /// <see cref="DbLocalizationOptions.ReturnKeyIfNotFound"/> is <c>true</c>, the <paramref name="name"/> is used
    /// as the format; otherwise an empty string is used.
    /// </returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = GetStringWithFallback(name) ?? (options.ReturnKeyIfNotFound ? name : string.Empty);
            var value = string.Format(format, arguments);
            var notFound = (format == name) || string.IsNullOrEmpty(format);

            return new LocalizedString(name, value, notFound);
        }
    }

    /// <summary>
    /// Returns all localized strings for the current UI culture and optionally parent cultures.
    /// Results are aggregated in order of culture priority (current culture first), and the first
    /// occurrence of a key takes precedence.
    /// </summary>
    /// <param name="includeParentCultures">
    /// When <c>true</c>, and when <see cref="DbLocalizationOptions.FallbackToParentCultures"/> is enabled,
    /// parent cultures are also queried (from nearest to farthest parent).
    /// </param>
    /// <returns>
    /// An enumerable of <see cref="LocalizedString"/> instances representing all resolved keys for the
    /// requested cultures (duplicates removed in favor of higher priority cultures).
    /// </returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        // For GetAllStrings we'll try current culture and, if requested, parents
        var cultures = new List<string> { CultureInfo.CurrentUICulture.Name };

        if (includeParentCultures && options.FallbackToParentCultures)
        {
            var parent = CultureInfo.CurrentUICulture.Parent;

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

        var results = new Dictionary<string, LocalizedString>();

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        foreach (var culture in cultures)
        {
            var list = db.Translations
                .Where(t => t.Resource == resource && t.Culture == culture)
                .AsEnumerable();

            foreach (var t in list)
            {
                // keep first occurrence (higher priority culture first)
                if (!results.ContainsKey(t.Key))
                {
                    results[t.Key] = new LocalizedString(t.Key, t.Value, resourceNotFound: false);
                }
            }
        }

        return results.Values.ToList();
    }

    /// <summary>
    /// Returns an <see cref="IStringLocalizer"/> scoped to the provided culture. This implementation
    /// relies on <see cref="CultureInfo.CurrentUICulture"/> for lookups and therefore simply returns
    /// the current instance. Callers who want culture-scoped behavior should set <see cref="CultureInfo.CurrentUICulture"/>
    /// before calling into this localizer.
    /// </summary>
    /// <param name="culture">The culture to scope to (not used by this implementation).</param>
    /// <returns>This instance (<c>this</c>).</returns>
    public IStringLocalizer WithCulture(CultureInfo culture) =>
        // We rely on CultureInfo.CurrentUICulture; to honor WithCulture we could clone options,
        // but for simplicity return this (callers can set CurrentUICulture externally).
        this;
}