namespace TinyLocalization.Entities;

/// <summary>
/// Represents a single localized translation entry.
/// </summary>
/// <remarks>
/// A <see cref="Translation"/> identifies a translated string by the combination of
/// resource name, key and culture. Instances are typically stored in a localization
/// database or other persistence store.
/// </remarks>
public class Translation
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