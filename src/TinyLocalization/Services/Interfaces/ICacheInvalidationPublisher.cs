namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Optional publisher interface used to broadcast cache invalidation events for database-backed translations.
/// Implementations are provided by the host application (for example a Redis-based pub/sub or message broker)
/// and enable distributed cache invalidation when translations change.
/// </summary>
/// <remarks>
/// The library defines this interface so consuming applications can implement and register a concrete publisher
/// to propagate invalidation events to other application instances. Implementations should be resilient and
/// avoid throwing exceptions that would disrupt the calling flow; callers typically fire-and-forget or await the task
/// depending on their requirements.
/// </remarks>
public interface ICacheInvalidationPublisher
{
    /// <summary>
    /// Publish an invalidation event for a single translation entry identified by resource, key and culture.
    /// </summary>
    /// <param name="resource">
    /// The logical resource name (for example a full type name or resource identifier) that scopes translations.
    /// </param>
    /// <param name="key">
    /// The translation key within the resource to invalidate.
    /// </param>
    /// <param name="culture">
    /// The culture name (for example "en" or "en-US") whose cached entry should be invalidated.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the publish operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the publish operation has been initiated or completed, depending on implementation.
    /// </returns>
    Task PublishSingleInvalidationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish an invalidation event for all translations belonging to the specified resource (all keys and cultures).
    /// </summary>
    /// <param name="resource">
    /// The logical resource name whose entire cache should be considered invalid and refreshed by subscribers.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the publish operation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the publish operation has been initiated or completed, depending on implementation.
    /// </returns>
    Task PublishResourceInvalidationAsync(string resource, CancellationToken cancellationToken = default);
}