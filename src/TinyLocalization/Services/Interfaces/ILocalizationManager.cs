namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Manager responsible for creating, retrieving and removing translations stored in the database,
/// and for publishing cache invalidation events when translations change.
/// </summary>
/// <remarks>
/// Implementations typically coordinate persistence (for example via a <c>LocalizationDbContext</c>),
/// cache population/eviction and notifying other application instances (via an <see cref="ICacheInvalidationPublisher"/>).
/// The interface surface is intentionally small and asynchronous to support I/O-bound operations.
/// </remarks>
public interface ILocalizationManager
{
    /// <summary>
    /// Retrieves a single translation value for the specified resource/key/culture.
    /// </summary>
    /// <param name="resource">
    /// Logical resource name that scopes translations (for example a full type name or resource identifier).
    /// </param>
    /// <param name="key">The translation key to lookup.</param>
    /// <param name="culture">The culture name (for example "en" or "en-US").</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The translation value if found; otherwise <see langword="null"/>.
    /// </returns>
    Task<string?> GetTranslationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all translations for the specified resource. Optionally restricts results to a single culture.
    /// </summary>
    /// <param name="resource">Logical resource name that scopes translations.</param>
    /// <param name="culture">
    /// Optional culture name to filter results. When <see langword="null"/>, translations for all cultures are returned.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// An enumerable of tuples containing <c>(Key, Value, Culture)</c> for each matching translation.
    /// Implementations should materialize results or stream them depending on usage patterns.
    /// </returns>
    Task<IEnumerable<(string Key, string Value, string Culture)>> GetAllTranslationsAsync(string resource, string? culture = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a translation value for the specified resource/key/culture.
    /// </summary>
    /// <param name="resource">Logical resource name that scopes translations.</param>
    /// <param name="key">The translation key to add or update.</param>
    /// <param name="culture">The culture name (for example "en" or "en-US").</param>
    /// <param name="value">The translation value to persist.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the operation finishes.</returns>
    Task SetTranslationAsync(string resource, string key, string culture, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a translation entry for the specified resource/key/culture.
    /// </summary>
    /// <param name="resource">Logical resource name that scopes translations.</param>
    /// <param name="key">The translation key to remove.</param>
    /// <param name="culture">The culture name whose entry should be removed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the deletion (if any) is finished.
    /// </returns>
    Task RemoveTranslationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes cache invalidation for a single translation entry or for the entire resource.
    /// </summary>
    /// <param name="resource">Logical resource name whose cache should be invalidated.</param>
    /// <param name="key">
    /// Optional translation key. When specified, only the single key/culture entry should be invalidated.
    /// When <see langword="null"/>, the entire resource is considered invalidated.
    /// </param>
    /// <param name="culture">
    /// Optional culture name. If provided alongside <paramref name="key"/>, only that culture's entry is invalidated.
    /// When <paramref name="culture"/> is <see langword="null"/> and <paramref name="key"/> is provided,
    /// implementations may choose to invalidate all cultures for the given key.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the invalidation message(s) have been published or submitted.
    /// </returns>
    Task PublishInvalidationAsync(string resource, string? key = null, string? culture = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a collection of translations into the store. Implementations should perform upserts
    /// and publish appropriate invalidation messages for affected resources/keys/cultures.
    /// </summary>
    /// <param name="translations">
    /// A collection of tuples representing translations in the form <c>(Resource, Key, Value, Culture)</c>.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the import has finished.</returns>
    Task ImportTranslationsAsync(IEnumerable<(string Resource, string Key, string Value, string Culture)> translations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports translations for the specified resource. Implementations may return an enumeration
    /// that consumers can iterate to serialize or persist externally.
    /// </summary>
    /// <param name="resource">Logical resource name to export.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// An enumerable of tuples in the form <c>(Key, Value, Culture)</c> representing exported translations.
    /// </returns>
    Task<IEnumerable<(string Key, string Value, string Culture)>> ExportTranslationsAsync(string resource, CancellationToken cancellationToken = default);
}