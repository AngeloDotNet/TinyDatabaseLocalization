using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TinyLocalization.Data;

namespace TinyLocalization.Localization;

/// <summary>
/// Database-backed implementation of <see cref="IStringLocalizer"/> that queries
/// translations from a <see cref="LocalizationDbContext"/>. A new IServiceScope is
/// created for each lookup to obtain a scoped <see cref="LocalizationDbContext"/> instance.
/// </summary>
/// <param name="serviceProvider">The root <see cref="IServiceProvider"/> used to create scopes and resolve the DB context.</param>
/// <param name="resource">The logical resource name used to scope translations (typically the resource or type name).</param>
public class EfStringLocalizer(IServiceProvider serviceProvider, string resource) : IStringLocalizer
{
    private readonly string resource = resource ?? string.Empty;

    /// <summary>
    /// Attempts to retrieve a translation value from the database for the given key and the current UI culture.
    /// The lookup first attempts an exact culture match (e.g., "en-US") and then falls back to parent cultures
    /// (e.g., "en") until the invariant culture is reached. A new scope and <see cref="LocalizationDbContext"/>
    /// are created for each attempted culture lookup.
    /// </summary>
    /// <param name="name">The translation key to look up.</param>
    /// <returns>
    /// The translated string if found; otherwise <see langword="null"/>.
    /// </returns>
    private string? GetStringFromDb(string name)
    {
        // Determine the culture (CurrentUICulture)
        var culture = CultureInfo.CurrentUICulture;

        // Try exact culture then fallback to parent cultures
        while (true)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();

            var cultureName = culture.Name;
            var entry = db.Translations.FirstOrDefault(t => t.Resource == resource && t.Key == name && t.Culture == cultureName);

            if (entry != null)
            {
                return entry.Value;
            }

            // If culture has parent (e.g., en-US -> en) try parent; if invariant (empty) stop
            if (string.IsNullOrEmpty(culture.Name) || string.IsNullOrEmpty(culture.Parent?.Name))
            {
                break;
            }

            culture = culture.Parent;

            if (culture == CultureInfo.InvariantCulture)
            {
                break;
            }
        }

        // not found
        return null;
    }

    /// <summary>
    /// Gets the localized string for the specified key using the current UI culture.
    /// </summary>
    /// <param name="name">The translation key.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> containing the translation if found; otherwise the key itself.
    /// The <see cref="LocalizedString.ResourceNotFound"/> flag is set to <see langword="true"/> when no translation was found.
    /// </returns>
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetStringFromDb(name);

            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

    /// <summary>
    /// Gets the localized, formatted string for the specified key and arguments using the current UI culture.
    /// If the translation is not found, the key is used as the format string.
    /// </summary>
    /// <param name="name">The translation key.</param>
    /// <param name="arguments">Format arguments to apply to the retrieved format string.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> containing the formatted translation (or formatted key when translation is missing).
    /// The <see cref="LocalizedString.ResourceNotFound"/> flag is set to <see langword="true"/> when no translation was found.
    /// </returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = GetStringFromDb(name) ?? name;
            var value = string.Format(format, arguments);
            var notFound = format == name;

            return new LocalizedString(name, value, notFound);
        }
    }

    /// <summary>
    /// Returns all translations for the current UI culture (and optionally its parent culture) for the configured resource.
    /// </summary>
    /// <param name="includeParentCultures">
    /// If <see langword="true"/>, translations for the immediate parent culture (if any) are included in the result set in addition
    /// to the current UI culture. Only a single-level parent is considered by this implementation.
    /// </param>
    /// <returns>
    /// An enumeration of <see cref="LocalizedString"/> containing key/value pairs for the matching translations.
    /// The returned collection is materialized into a <see cref="List{T}"/>.
    /// </returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        using var scope = serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<LocalizationDbContext>();
        var query = db.Translations.Where(t => t.Resource == resource && t.Culture == culture);

        // If includeParentCultures requested, also include parent culture entries
        if (includeParentCultures)
        {
            var parent = CultureInfo.CurrentUICulture.Parent;

            if (!string.IsNullOrEmpty(parent?.Name))
            {
                var parentName = parent.Name;
                query = query.Concat(db.Translations.Where(t => t.Resource == resource && t.Culture == parentName));
            }
        }

        return query.AsEnumerable().Select(t => new LocalizedString(t.Key, t.Value, false)).ToList();
    }

    /// <summary>
    /// Returns an <see cref="IStringLocalizer"/> for the specified culture.
    /// </summary>
    /// <param name="culture">The culture for which a localizer is requested.</param>
    /// <remarks>
    /// This implementation does not produce culture-specific instances. Consumers should set <see cref="CultureInfo.CurrentUICulture"/>
    /// externally before calling into this localizer. Consequently this method returns the same instance.
    /// </remarks>
    /// <returns>The current <see cref="IStringLocalizer"/> instance.</returns>
    public IStringLocalizer WithCulture(CultureInfo culture) => this;
}