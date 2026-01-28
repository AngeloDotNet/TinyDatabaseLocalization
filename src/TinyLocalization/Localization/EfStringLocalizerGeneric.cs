using Microsoft.Extensions.Localization;

namespace TinyLocalization.Localization;

/// <summary>
/// Generic wrapper so that an <see cref="IStringLocalizer{T}"/> can be injected and
/// forwarded to the factory-created <see cref="IStringLocalizer"/> for the specified type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type used to create the underlying localizer via the <see cref="IStringLocalizerFactory"/>.</typeparam>
/// <param name="factory">The factory used to create the inner <see cref="IStringLocalizer"/> instance for <typeparamref name="T"/>.</param>
public class EfStringLocalizerGeneric<T>(IStringLocalizerFactory factory) : IStringLocalizer
{
    /// <summary>
    /// The inner localizer instance created by the provided <see cref="IStringLocalizerFactory"/>.
    /// All interface calls are forwarded to this instance.
    /// </summary>
    private readonly IStringLocalizer inner = factory.Create(typeof(T));

    /// <summary>
    /// Gets the localized string with the specified <paramref name="name"/>.
    /// This indexer forwards the request to the inner localizer.
    /// </summary>
    /// <param name="name">The resource name to localize.</param>
    /// <returns>A <see cref="LocalizedString"/> representing the localized value (or the name if not found).</returns>
    public LocalizedString this[string name] => inner[name];

    /// <summary>
    /// Gets the localized string with the specified <paramref name="name"/>, formatting it with the provided arguments.
    /// This indexer forwards the request to the inner localizer.
    /// </summary>
    /// <param name="name">The resource name to localize.</param>
    /// <param name="arguments">An array of objects to format into the localized string.</param>
    /// <returns>A <see cref="LocalizedString"/> representing the formatted localized value.</returns>
    public LocalizedString this[string name, params object[] arguments] => inner[name, arguments];

    /// <summary>
    /// Returns all strings from the underlying localizer.
    /// </summary>
    /// <param name="includeParentCultures">
    /// If <see langword="true"/>, strings from parent cultures are included; otherwise only the current culture's strings are returned.
    /// </param>
    /// <returns>An enumeration of <see cref="LocalizedString"/> instances.</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
}