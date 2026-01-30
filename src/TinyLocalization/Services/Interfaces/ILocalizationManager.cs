using TinyLocalization.Entities;

namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Defines methods for managing localization resources and translations in an asynchronous manner.
/// </summary>
/// <remarks>The ILocalizationManager interface provides a contract for adding, updating, removing, and
/// invalidating translations for different resources and cultures. It is designed to support efficient localization
/// management in applications that require dynamic or runtime translation updates. Implementations should ensure thread
/// safety and handle cancellation tokens appropriately to support responsive and scalable localization
/// operations.</remarks>
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
    /// Adds a new translation or updates an existing translation entry in the localization system.
    /// </summary>
    /// <remarks>If a translation entry matching the resource, key, and culture already exists, its value is
    /// updated; otherwise, a new entry is created. Ensure that the translation object contains valid and complete
    /// information before calling this method.</remarks>
    /// <param name="translation">The translation object containing the resource, key, value, and culture information to add or update. Cannot be
    /// null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous add or update operation.</returns>
    Task AddOrUpdateAsync(Translation translation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a resource entry identified by the specified key and culture from the given resource set.
    /// </summary>
    /// <remarks>If the specified resource, key, or culture does not exist, the method returns <see
    /// langword="false"/>. The operation can be canceled by passing a cancellation token.</remarks>
    /// <param name="resource">The name of the resource set from which to remove the entry. Cannot be null or empty.</param>
    /// <param name="key">The unique key that identifies the resource entry to remove. Cannot be null or empty.</param>
    /// <param name="culture">The culture code that specifies the culture of the resource entry to remove. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the resource
    /// entry was successfully removed; otherwise, <see langword="false"/>.</returns>
    Task<bool> RemoveAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously invalidates the specified resource, ensuring that any cached data is refreshed on the next
    /// access.
    /// </summary>
    /// <remarks>This method may take additional time to complete depending on the current state of the cache
    /// and the resource being invalidated. Callers should handle cancellation appropriately to avoid unnecessary work
    /// if the operation is no longer required.</remarks>
    /// <param name="resource">The name of the resource to invalidate. This must correspond to a valid resource identifier present in the
    /// cache.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation of invalidating the resource.</returns>
    Task InvalidateResourceAsync(string resource, CancellationToken cancellationToken = default);
}