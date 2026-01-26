using Microsoft.Extensions.Localization;

namespace TinyLocalization.Localization;

/// <summary>
/// Factory that creates <see cref="EfStringLocalizer"/> instances.
/// The factory holds a root <see cref="IServiceProvider"/> which is used by created localizers
/// to create scopes and resolve a scoped <see cref="TinyLocalization.Data.LocalizationDbContext"/>.
/// </summary>
/// <param name="serviceProvider">
/// The root <see cref="IServiceProvider"/> used to create scopes. Typically this is the application's
/// service provider registered during startup.
/// </param>
public class EfStringLocalizerFactory(IServiceProvider serviceProvider) : IStringLocalizerFactory
{
    /// <summary>
    /// Creates an <see cref="IStringLocalizer"/> for the specified resource <see cref="Type"/>.
    /// </summary>
    /// <param name="resourceSource">
    /// The <see cref="Type"/> representing the resource source (commonly a class or type whose name
    /// is used as the logical resource identifier). The factory will use <see cref="Type.FullName"/>
    /// when available, otherwise <see cref="Type.Name"/>.
    /// </param>
    /// <returns>
    /// An <see cref="IStringLocalizer"/> instance scoped to the resolved resource key.
    /// </returns>
    public IStringLocalizer Create(Type resourceSource)
    {
        var resource = resourceSource.FullName ?? resourceSource.Name;

        return new EfStringLocalizer(serviceProvider, resource);
    }

    /// <summary>
    /// Creates an <see cref="IStringLocalizer"/> for the specified resource name and location.
    /// </summary>
    /// <param name="baseName">
    /// The base resource name (for example, a logical name such as "Shared" or a class name).
    /// </param>
    /// <param name="location">
    /// An optional location or namespace prefix. When specified, the final resource key is formed
    /// by combining <paramref name="location"/> and <paramref name="baseName"/> as
    /// "<c>{location}.{baseName}</c>".
    /// </param>
    /// <returns>
    /// An <see cref="IStringLocalizer"/> instance scoped to the computed resource key.
    /// </returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        // baseName could be e.g. "Shared", we'll combine baseName/location to form a resource key
        var resource = string.IsNullOrEmpty(location) ? baseName : $"{location}.{baseName}";

        return new EfStringLocalizer(serviceProvider, resource);
    }
}