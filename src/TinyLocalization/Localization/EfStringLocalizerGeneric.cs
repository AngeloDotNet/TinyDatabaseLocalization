using Microsoft.Extensions.Localization;

namespace TinyLocalization.Localization;

/// <summary>
/// Generic wrapper that implements <see cref="IStringLocalizer"/> and forwards all calls
/// to a non-generic <see cref="IStringLocalizer"/> created by an <see cref="IStringLocalizerFactory"/>
/// for the specified resource type <typeparamref name="T"/>.
/// This enables injection of <see cref="IStringLocalizer{T}"/> in consuming code.
/// </summary>
/// <typeparam name="T">
/// The resource type used by the factory to determine the logical resource key (typically
/// <see cref="Type.FullName"/> or <see cref="Type.Name"/> of <typeparamref name="T"/>).
/// </typeparam>
/// <param name="factory">The <see cref="IStringLocalizerFactory"/> used to create the inner localizer.</param>
public class EfStringLocalizerGeneric<T>(IStringLocalizerFactory factory) : IStringLocalizer
{
    /// <summary>
    /// The underlying non-generic localizer created for <typeparamref name="T"/>.
    /// All members of this wrapper delegate to this instance.
    /// </summary>
    private readonly IStringLocalizer inner = factory.Create(typeof(T));

    /// <summary>
    /// Gets the localized string for the specified key by delegating to <see cref="inner"/>.
    /// The returned <see cref="LocalizedString"/> will indicate whether the resource was found
    /// via its <see cref="LocalizedString.ResourceNotFound"/> property.
    /// </summary>
    /// <param name="name">The resource key to look up.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> containing the localized value or the key itself if not found.
    /// </returns>
    public LocalizedString this[string name] => inner[name];

    /// <summary>
    /// Gets the localized, formatted string for the specified key and arguments by delegating to <see cref="inner"/>.
    /// If the translation is not found, behavior (formatting and fallback) is delegated to the inner localizer.
    /// </summary>
    /// <param name="name">The resource key to look up.</param>
    /// <param name="arguments">Format arguments to apply to the retrieved format string.</param>
    /// <returns>
    /// A <see cref="LocalizedString"/> containing the formatted localized value or a formatted key when translation is missing.
    /// </returns>
    public LocalizedString this[string name, params object[] arguments] => inner[name, arguments];

    /// <summary>
    /// Returns all localized strings from the inner localizer for the current UI culture.
    /// </summary>
    /// <param name="includeParentCultures">
    /// If <see langword="true"/>, translations from parent cultures may be included; the exact behavior is delegated to the inner localizer.
    /// </param>
    /// <returns>An enumerable of <see cref="LocalizedString"/> containing available translations.</returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);
}