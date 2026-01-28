using Microsoft.Extensions.Localization;
using TinyLocalization.Options;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Localization;

/// <summary>
/// Factory responsible for creating <see cref="EfStringLocalizer"/> instances.
/// </summary>
/// <remarks>
/// This factory captures an <see cref="IServiceProvider"/>, an <see cref="IFusionCache"/>,
/// and <see cref="DbLocalizationOptions"/> and uses them to create localized string providers
/// bound to a specific resource key.
/// </remarks>
/// <param name="serviceProvider">The application's service provider used to resolve services when creating localizers.</param>
/// <param name="fusionCache">A shared <see cref="IFusionCache"/> instance used by created localizers for caching.</param>
/// <param name="options">Database localization options used to configure behavior of created localizers.</param>
public class EfStringLocalizerFactory(IServiceProvider serviceProvider, IFusionCache fusionCache, DbLocalizationOptions options) : IStringLocalizerFactory
{
    /// <summary>
    /// Creates an <see cref="IStringLocalizer"/> for the specified resource source type.
    /// </summary>
    /// <param name="resourceSource">
    /// The type that identifies the resource. The factory uses <see cref="Type.FullName"/> if available,
    /// otherwise <see cref="Type.Name"/> to form the resource key.
    /// </param>
    /// <returns>
    /// A new <see cref="EfStringLocalizer"/> instance scoped to the resource identified by <paramref name="resourceSource"/>.
    /// </returns>
    public IStringLocalizer Create(Type resourceSource)
    {
        var resource = resourceSource.FullName ?? resourceSource.Name;
        return new EfStringLocalizer(serviceProvider, resource, fusionCache, options);
    }

    /// <summary>
    /// Creates an <see cref="IStringLocalizer"/> for the specified base name and location.
    /// </summary>
    /// <param name="baseName">
    /// The base name of the resource (for example, a shared resource name).
    /// </param>
    /// <param name="location">
    /// The location (typically the assembly or namespace) of the resource. If <see cref="string.Empty"/> or <c>null</c>,
    /// the <paramref name="baseName"/> is used as the resource key; otherwise <c>&lt;location&gt;.&lt;baseName&gt;</c> is used.
    /// </param>
    /// <returns>
    /// A new <see cref="EfStringLocalizer"/> instance scoped to the composed resource key.
    /// </returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        // baseName could be e.g. "Shared", we'll combine baseName/location to form a resource key
        var resource = string.IsNullOrEmpty(location) ? baseName : $"{location}.{baseName}";
        return new EfStringLocalizer(serviceProvider, resource, fusionCache, options);
    }
}