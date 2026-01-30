using Microsoft.Extensions.Localization;

namespace TinyLocalization.Localization;

/// <summary>
/// Provides a type-specific implementation of <see cref="IStringLocalizer"/> that retrieves localized strings for the
/// specified resource type using the given <see cref="IStringLocalizerFactory"/>.
/// </summary>
/// <remarks>This class enables localization scenarios where resources are organized by type, allowing consumers
/// to obtain localized strings for a specific class or feature. All localization operations are delegated to the inner
/// localizer instance created by the factory, ensuring consistency with the application's localization
/// infrastructure.</remarks>
/// <typeparam name="T">The resource type for which localization is provided. Typically, this is the type associated with the resource file
/// containing localized strings.</typeparam>
/// <param name="factory">The factory used to create the underlying <see cref="IStringLocalizer"/> instance for the specified resource type.
/// Cannot be null.</param>
public class EfStringLocalizerGeneric<T>(IStringLocalizerFactory factory) : IStringLocalizer
{
    /// <summary>
    /// Provides access to the string localizer instance used for localizing strings in the context of the specified
    /// type parameter.
    /// </summary>
    /// <remarks>This localizer is created using the provided factory and is intended for use in scenarios
    /// where localization resources are associated with a specific type. The factory should be configured to supply the
    /// appropriate localization strategy for the application's needs.</remarks>
    private readonly IStringLocalizer inner = factory.Create(typeof(T));

    /// <summary>
    /// Gets the localized string associated with the specified name.
    /// </summary>
    /// <param name="name">The name of the localized string to retrieve.</param>
    /// <returns>A <see cref="LocalizedString"/> representing the localized string for the specified name. Returns an empty
    /// string if the name does not exist.</returns>
    public LocalizedString this[string name] => inner[name];

    /// <summary>
    /// Gets the localized string associated with the specified name, formatted with the provided arguments.
    /// </summary>
    /// <remarks>This indexer allows for dynamic retrieval and formatting of localized strings, making it
    /// useful for applications that require localization support.</remarks>
    /// <param name="name">The name of the localized string to retrieve.</param>
    /// <param name="arguments">An array of objects to format the localized string with. Each object is used as a placeholder in the string.</param>
    /// <returns>A <see cref="LocalizedString"/> representing the formatted localized string based on the specified name and
    /// arguments.</returns>
    public LocalizedString this[string name, params object[] arguments] => inner[name, arguments];

    /// <summary>
    /// Retrieves all localized string resources for the current culture, with an option to include resources from
    /// parent cultures.
    /// </summary>
    /// <remarks>Use this method to obtain a comprehensive set of localized strings, including those inherited
    /// from parent cultures when fallback is desired.</remarks>
    /// <param name="includeParentCultures">true to include localized strings from parent cultures in addition to the current culture; otherwise, false to
    /// return only strings for the current culture.</param>
    /// <returns>An enumerable collection of LocalizedString objects representing the available localized strings for the
    /// specified culture.</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
}