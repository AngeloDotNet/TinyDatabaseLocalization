namespace TinyLocalization.Services.Interfaces;

/// <summary>
/// Defines a contract for publishing cache invalidation notifications to ensure that cached resources are updated or
/// removed as necessary across the system.
/// </summary>
/// <remarks>Implementations of this interface are responsible for notifying subscribers about cache invalidation
/// events, which is essential for maintaining data consistency, especially in distributed or multi-instance
/// environments. Typical use cases include propagating changes to localized resources or other shared data that may be
/// cached in multiple locations.</remarks>
public interface ICacheInvalidationPublisher
{
    /// <summary>
    /// Publishes an invalidation notification for a specific resource, key, and culture asynchronously.
    /// </summary>
    /// <remarks>Use this method to invalidate cached or stored localization resources when updates occur,
    /// ensuring that changes are reflected promptly in the application.</remarks>
    /// <param name="resource">The identifier of the resource to invalidate. Cannot be null or empty.</param>
    /// <param name="key">The key associated with the resource that should be invalidated. Cannot be null or empty.</param>
    /// <param name="culture">The culture code that specifies the localization context for the invalidation. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation of publishing the invalidation notification.</returns>
    Task PublishSingleInvalidationAsync(string resource, string key, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an invalidation request for the specified resource using the provided invalidation keys.
    /// </summary>
    /// <remarks>This method is asynchronous and may take additional time to complete depending on the number
    /// of keys and the state of the resource. Ensure that the resource identifier and keys are valid before calling
    /// this method.</remarks>
    /// <param name="resource">The identifier of the resource to be invalidated. This parameter cannot be null or empty.</param>
    /// <param name="keys">A collection of invalidation keys that specify the items to invalidate. This collection must not be null and
    /// must contain at least one key.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation of publishing the invalidation request.</returns>
    Task PublishResourceInvalidationAsync(string resource, IEnumerable<InvalidationKey> keys, CancellationToken cancellationToken = default);
}