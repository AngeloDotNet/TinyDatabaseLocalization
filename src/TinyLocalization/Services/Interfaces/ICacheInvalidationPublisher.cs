namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Optional publisher interface used by the library to publish cache invalidation events.
/// Implementations are provided by the host application (for example, a Redis-based publisher)
/// and are responsible for notifying subscribers that cached translations should be invalidated.
/// </summary>
public interface ICacheInvalidationPublisher
{
    /// <summary>
    /// Publishes an invalidation event for a single cached translation entry identified by
    /// the resource name, key and culture.
    /// </summary>
    /// <param name="resource">The logical resource (e.g. resource/table name) that contains the translation.</param>
    /// <param name="key">The specific translation key within <paramref name="resource"/> to invalidate.</param>
    /// <param name="culture">The culture (for example "en-US") of the translation to invalidate.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
    Task PublishSingleInvalidationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an invalidation event for an entire resource. The publisher receives an explicit
    /// collection of keys (and their cultures) so subscribers can remove the corresponding cache entries
    /// without querying the database for the list of keys.
    /// </summary>
    /// <param name="resource">The logical resource (e.g. resource/table name) whose keys are being invalidated.</param>
    /// <param name="keys">
    /// An enumeration of <see cref="InvalidationKey"/> instances describing the specific keys and cultures
    /// to invalidate for the specified resource.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
    Task PublishResourceInvalidationAsync(string resource, IEnumerable<InvalidationKey> keys, CancellationToken cancellationToken = default);
}