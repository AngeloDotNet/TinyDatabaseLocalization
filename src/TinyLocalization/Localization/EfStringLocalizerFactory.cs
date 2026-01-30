using Microsoft.Extensions.Localization;
using TinyLocalization.Options;
using ZiggyCreatures.Caching.Fusion;

namespace TinyLocalization.Localization;

/// <summary>
/// Provides a factory for creating instances of <see cref="IStringLocalizer"/> that retrieve localized strings from a
/// database using Entity Framework, supporting resource-based and location-based localization strategies.
/// </summary>
/// <remarks>This factory enables flexible localization by allowing the creation of localizers based on resource
/// types or explicit resource keys. It supports caching and custom configuration to optimize performance and adapt to
/// various localization scenarios.</remarks>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve dependencies required for localization services.</param>
/// <param name="fusionCache">The <see cref="IFusionCache"/> instance used to cache localized strings and improve lookup performance.</param>
/// <param name="options">The <see cref="DbLocalizationOptions"/> that configure the behavior and settings of the localization services.</param>
public class EfStringLocalizerFactory(IServiceProvider serviceProvider, IFusionCache fusionCache, DbLocalizationOptions options) : IStringLocalizerFactory
{
    /// <summary>
    /// Creates a new instance of an <see cref="IStringLocalizer"/> that provides localized strings for the specified
    /// resource source type.
    /// </summary>
    /// <remarks>The resource name is derived from the full name of the <paramref name="resourceSource"/>
    /// type. If the full name is not available, the type's name is used instead.</remarks>
    /// <param name="resourceSource">The type that serves as the source for localization resources. This type is used to determine the resource name
    /// for localization.</param>
    /// <returns>An <see cref="IStringLocalizer"/> instance that can be used to retrieve localized strings based on the specified
    /// resource source type.</returns>
    public IStringLocalizer Create(Type resourceSource)
    {
        var resource = resourceSource.FullName ?? resourceSource.Name;
        return new EfStringLocalizer(serviceProvider, resource, fusionCache, options);
    }

    /// <summary>
    /// Creates a new instance of an <see cref="IStringLocalizer"/> for the specified resource base name and location.
    /// </summary>
    /// <remarks>The resource key is constructed by combining the location and base name, separated by a dot,
    /// if the location is provided. This enables organized management of resources across different
    /// locations.</remarks>
    /// <param name="baseName">The base name of the resource to localize. Typically represents a specific resource set, such as a class or
    /// shared resource group. Cannot be null or empty.</param>
    /// <param name="location">The location of the resource, used to further qualify the resource key. If null or empty, only the base name is
    /// used.</param>
    /// <returns>An <see cref="IStringLocalizer"/> instance that retrieves localized strings based on the specified resource key.</returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        // baseName could be e.g. "Shared", we'll combine baseName/location to form a resource key
        var resource = string.IsNullOrEmpty(location) ? baseName : $"{location}.{baseName}";
        return new EfStringLocalizer(serviceProvider, resource, fusionCache, options);
    }
}