using System.Globalization;
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
/// culture, and leverages caching to optimize repeated lookups. The localizer is intended for use in ASP.NET Core and
/// other .NET applications requiring dynamic, database-driven localization.</remarks>
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
    /// Stores the resource string, defaulting to an empty string if the provided value is null.
    /// </summary>
    private readonly string resource = resource ?? string.Empty;

    /// <summary>
    /// Retrieves the localized string value from the database for the specified resource key and culture without using
    /// a cache.
    /// </summary>
    /// <remarks>This method does not cache the retrieved value, meaning each call results in a database
    /// query.</remarks>
    /// <param name="name">The key of the resource for which the localized string is requested. This parameter cannot be null.</param>
    /// <param name="cultureName">The culture name that specifies the localization context. This parameter cannot be null.</param>
    /// <returns>The localized string value associated with the specified key and culture, or null if no matching entry is found.</returns>
    private string? GetStringFromDb_NoCache(string name, string cultureName)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

        var entry = db.Translations.FirstOrDefault(t => t.Resource == resource && t.Key == name && t.Culture == cultureName);

        return entry?.Value;
    }

    /// <summary>
    /// Retrieves a localized string for the specified resource name, attempting to resolve it through the current UI
    /// culture and configured fallback cultures.
    /// </summary>
    /// <remarks>The method first attempts to find the resource string in the current UI culture. If not
    /// found, it falls back to parent cultures, a global fallback culture if configured, and finally the invariant
    /// culture. Each lookup is cached per culture to improve performance. The method does not throw if the resource is
    /// not found; instead, it returns null.</remarks>
    /// <param name="name">The name of the resource string to retrieve. Cannot be null.</param>
    /// <returns>The localized string associated with the specified name, or null if the string is not found in any of the
    /// attempted cultures.</returns>
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
    /// Generates a cache key that uniquely identifies a localized resource entry based on the specified resource, name,
    /// and culture information.
    /// </summary>
    /// <remarks>If <paramref name="cultureName"/> is an empty string, an underscore ("_") is used in the
    /// cache key to represent the invariant culture. This ensures that cache keys are consistently formatted and avoids
    /// empty segments.</remarks>
    /// <param name="resource">The identifier of the resource for which the cache key is being created. This typically represents the resource
    /// set or source.</param>
    /// <param name="name">The name of the specific resource entry to include in the cache key.</param>
    /// <param name="cultureName">The culture name used to distinguish localized entries. If an empty string is provided, the invariant culture is
    /// assumed.</param>
    /// <returns>A formatted string that serves as the cache key, incorporating the cache key prefix, resource, name, and culture
    /// segment.</returns>
    private string BuildCacheKey(string resource, string name, string cultureName)
    {
        // Use prefix so cache keys are grouped and easier to remove
        // Note: cultureName can be empty string for invariant; normalize to "_" to avoid empty segments if desired
        var cultureSegment = string.IsNullOrEmpty(cultureName) ? "_" : cultureName;

        return $"{options.CacheKeyPrefix}:{resource}:{name}:{cultureSegment}";
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
    /// string is returned. The method uses a fallback mechanism to attempt to retrieve the string.</remarks>
    /// <param name="name">The key that identifies the localized string to retrieve.</param>
    /// <param name="arguments">An array of objects to format the localized string with. Each object is used to replace a corresponding
    /// placeholder in the localized string.</param>
    /// <returns>A LocalizedString object containing the key, the formatted localized value, and a flag indicating whether the
    /// string was found.</returns>
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
    /// Retrieves all localized strings for the current user interface culture, with an option to include strings from
    /// parent cultures and a global fallback culture if configured.
    /// </summary>
    /// <remarks>If includeParentCultures is set to true and fallback to parent cultures is enabled, the
    /// method searches through the hierarchy of parent cultures in order of specificity. If a global fallback culture
    /// is configured and not already included, it is also searched. The returned collection contains only the first
    /// occurrence of each key, prioritizing more specific cultures.</remarks>
    /// <param name="includeParentCultures">true to include localized strings from parent cultures in addition to the current culture; otherwise, false.</param>
    /// <returns>A collection of LocalizedString objects representing the localized strings available for the specified cultures.
    /// Each key appears only once, with values from the highest-priority culture.</returns>
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
    /// Returns a localizer instance that uses the specified culture for resource lookups.
    /// </summary>
    /// <remarks>This method does not create a new localizer or clone localization options. Instead, it relies
    /// on the caller to set CultureInfo.CurrentUICulture to the desired culture before performing resource lookups.
    /// Ensure that CurrentUICulture is set appropriately to reflect the intended culture.</remarks>
    /// <param name="culture">The culture to use for localizing resources. This parameter determines the language and regional formatting
    /// applied to localized strings.</param>
    /// <returns>An instance of IStringLocalizer configured to use the specified culture for resource lookups.</returns>
    public IStringLocalizer WithCulture(CultureInfo culture) =>
        // We rely on CultureInfo.CurrentUICulture; to honor WithCulture we could clone options,
        // but for simplicity return this (callers can set CurrentUICulture externally).
        this;
}