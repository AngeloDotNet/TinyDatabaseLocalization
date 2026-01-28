using TinyLocalization.Entities;

namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Manages translation data and cache invalidation operations for the library.
/// Implementations are responsible for persisting <see cref="Translation"/> entities,
/// removing translations, and triggering invalidation of cached translations when needed.
/// </summary>
public interface ILocalizationManager
{
    //Task<string?> GetTranslationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);
    //Task<IEnumerable<(string Key, string Value, string Culture)>> GetAllTranslationsAsync(string resource, string? culture = null, CancellationToken cancellationToken = default);
    //Task SetTranslationAsync(string resource, string key, string culture, string value, CancellationToken cancellationToken = default);
    //Task RemoveTranslationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);
    //Task PublishInvalidationAsync(string resource, string? key = null, string? culture = null, CancellationToken cancellationToken = default);
    //Task ImportTranslationsAsync(IEnumerable<(string Resource, string Key, string Value, string Culture)> translations, CancellationToken cancellationToken = default);
    //Task<IEnumerable<(string Key, string Value, string Culture)>> ExportTranslationsAsync(string resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new <see cref="Translation"/> or updates an existing one.
    /// Implementations should insert the provided <paramref name="translation"/> if it does not exist,
    /// or update the existing entry that matches its identifying properties.
    /// </summary>
    /// <param name="translation">The <see cref="Translation"/> entity to add or update.</param>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> that completes when the operation finishes.</returns>
    Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a translation entry identified by the specified <paramref name="resource"/>, <paramref name="key"/> and <paramref name="culture"/>.
    /// </summary>
    /// <param name="resource">The logical resource (for example a resource/table name) containing the translation.</param>
    /// <param name="key">The translation key to remove.</param>
    /// <param name="culture">The culture (for example "en-US") of the translation to remove.</param>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to observe while waiting for the operation to complete.</param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.Task{TResult}"/> that returns <see langword="true"/> if a translation was removed;
    /// otherwise <see langword="false"/> if no matching translation was found.
    /// </returns>
    Task<bool> RemoveAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached translations for the specified <paramref name="resource"/>.
    /// Implementations may clear local caches and/or publish invalidation events to notify
    /// other subscribers (for example through an <c>ICacheInvalidationPublisher</c>).
    /// </summary>
    /// <param name="resource">The logical resource whose cached translations should be invalidated.</param>
    /// <param name="cancellationToken">A <see cref="System.Threading.CancellationToken"/> to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> that completes when the invalidation process finishes.</returns>
    Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default);
}