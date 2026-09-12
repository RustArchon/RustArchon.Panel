// Copyright ©2026 Scott Blomfield

using Microsoft.Extensions.Localization;

namespace RustArchon.Panel.Localization;

/// <summary>
/// Hands out <see cref="JsonFileStringLocalizer"/> instances, all reading from the one
/// <c>Resources/</c> folder next to the built app - see <see cref="JsonFileStringLocalizer"/> for why
/// this exists instead of a package, and <c>Resources/README.md</c> for the shared-resource pattern
/// this backs (one <c>SharedResource.&lt;culture&gt;.json</c> per language, not one per component).
/// </summary>
public class JsonFileStringLocalizerFactory(IWebHostEnvironment environment) : IStringLocalizerFactory
{
    private readonly string _resourcesDirectory = Path.Combine(environment.ContentRootPath, "Resources");

    /// <inheritdoc />
    /// <remarks>Every component that injects <c>IStringLocalizer&lt;SomeType&gt;</c> lands here with
    /// <c>resourceSource</c> set to that type - the shared-resource pattern means everything actually
    /// requests <see cref="SharedResource"/>, but nothing stops a future one-off resource type from
    /// working the same way (its own <c>&lt;TypeName&gt;.&lt;culture&gt;.json</c> file).</remarks>
    public IStringLocalizer Create(Type resourceSource) => Create(resourceSource.Name, string.Empty);

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location) =>
        new JsonFileStringLocalizer(_resourcesDirectory, baseName);
}
