// Copyright ©2026 Scott Blomfield

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace RustArchon.Panel.Localization;

/// <summary>
/// <see cref="IStringLocalizer"/> backed by plain <c>Resources/&lt;baseName&gt;.&lt;culture&gt;.json</c>
/// files - flat <c>{"key": "value"}</c> objects, one per language, read straight off disk rather than
/// embedded. See <see cref="JsonFileStringLocalizerFactory"/> for how a component actually gets one of
/// these, and <c>Resources/README.md</c> for the whole story (Weblate, adding a language, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Written after two different behaviors of a third-party JSON-localizer package turned out to
/// contradict its own documentation (a flat-dictionary example that wasn't actually the format its
/// code deserialized into, then a still-unresolved failure to find a non-default culture's file at
/// all) - each discovered only by reading its source directly. A file read, a dictionary lookup, and a
/// culture-fallback loop is little enough code, and low enough risk, that owning it beats depending on
/// a package whose actual behavior twice failed to match what it said about itself. The <em>interface</em>
/// this sits behind - <see cref="IStringLocalizer{T}"/>, wired up by the standard
/// <c>Microsoft.Extensions.Localization</c>/<c>RequestLocalizationOptions</c> pipeline in
/// <c>Program.cs</c> - is exactly as standard as if the package had worked: nothing about how a
/// <c>.razor</c> file calls <c>Localizer["Key"]</c> is bespoke, only where the values come from.
/// </para>
/// <para>
/// Keys are the English source text itself (<c>"Home": "Home"</c> in the English file) - so a missing
/// translation and "fall back to English" are the same thing by construction:
/// <see cref="GetString"/>'s culture-fallback loop bottoming out with nothing found just returns the
/// key, which already <em>is</em> the English string.
/// </para>
/// </remarks>
public class JsonFileStringLocalizer(string resourcesDirectory, string baseName) : IStringLocalizer
{
    // Keyed by "baseName.cultureName" - process-lifetime, not a TTL cache. A translation change reaches
    // production through a real deploy (Weblate opens a PR; merging it is the deploy step - see
    // Resources/README.md), never a hot-edited file underneath a running process, so there is nothing
    // to invalidate.
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = this[name];
            return new LocalizedString(
                name, string.Format(CultureInfo.CurrentUICulture, format.Value, arguments), format.ResourceNotFound);
        }
    }

    /// <inheritdoc />
    /// <remarks>Every key in the current culture's own file, not the merged result of walking the
    /// fallback chain - good enough for the one thing this is realistically used for (an admin-facing
    /// "what's translated" listing), not relied on anywhere <c>this[name]</c> itself is.</remarks>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        LoadForCulture(CultureInfo.CurrentUICulture).Select(pair => new LocalizedString(pair.Key, pair.Value));

    private string? GetString(string name)
    {
        for (var culture = CultureInfo.CurrentUICulture;
             culture != CultureInfo.InvariantCulture;
             culture = culture.Parent)
        {
            if (LoadForCulture(culture).TryGetValue(name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private IReadOnlyDictionary<string, string> LoadForCulture(CultureInfo culture) =>
        Cache.GetOrAdd($"{baseName}.{culture.Name}", _ => ReadFile(culture.Name));

    private Dictionary<string, string> ReadFile(string cultureName)
    {
        var path = Path.Combine(resourcesDirectory, $"{baseName}.{cultureName}.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        }
        catch (JsonException)
        {
            // Malformed JSON in one language's file shouldn't take down every string on the page -
            // callers already treat "nothing found here" as "fall back to the next culture in the
            // chain, and ultimately to the key itself" (see GetString), which is the right answer for
            // a broken file too.
            return [];
        }
    }
}
