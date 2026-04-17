using TinyLocalization.Entities.Interfaces;

namespace TinyLocalization.Entities;

/// <summary>
/// Represents a localized translation entry for a specific resource, key, and culture.
/// </summary>
/// <remarks>A Translation instance associates a resource name, a key, and a culture with a translated string
/// value. This class is typically used in localization scenarios to provide language-specific text for application
/// resources.</remarks>
public class Translation : IEntity<int>
{
    /// <summary>
    /// Gets or sets the primary key identifier for this translation entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the resource name that this translation belongs to.
    /// </summary>
    /// <example>
    /// Examples: the full type name of a resource owner or the string "Shared".
    /// </example>
    public string Resource { get; set; } = null!;

    /// <summary>
    /// Gets or sets the key identifying the string to translate.
    /// </summary>
    /// <example>
    /// Example: "Hello".
    /// </example>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the culture name for this translation.
    /// </summary>
    /// <example>
    /// Examples: "en", "en-US", "it".
    /// </example>
    public string Culture { get; set; } = null!;

    /// <summary>
    /// Gets or sets the translated value for the specified resource/key/culture.
    /// </summary>
    public string Value { get; set; } = null!;
}