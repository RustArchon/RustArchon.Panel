// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Infrastructure;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// The platform's own display name and public site URL - read straight from the same Valkey cache
/// <c>RustArchon.Api</c>'s own <c>PlatformSettingsCache</c> writes through to on every admin save (see
/// <see cref="IValkeyCache"/>), not a second, Panel-local cache of its own. A save takes effect
/// everywhere - every open tab, every Panel instance - the moment the Api's write-through lands,
/// because there is nothing here to separately invalidate.
/// </summary>
/// <remarks>
/// <see cref="ISiteBrandingApiClient"/> (the anonymous <c>PublicBrandingController</c> endpoint) is
/// only the fallback, for the one case Valkey alone cannot answer: the key genuinely isn't cached yet
/// (Valkey has no durable volume - a restart empties it, and nothing repopulates an entry until
/// something reads it through the Api). That Api call already carries its own Postgres-backed
/// fallback and, as a side effect of answering it, repopulates Valkey for the next reader here.
/// </remarks>
public class SiteBrandingService(IValkeyCache cache, ISiteBrandingApiClient client)
{
    // Must match RustArchon.Api's PlatformSettingsCache.CacheKeyFor exactly - this reads the same
    // cache entries that class writes, not a parallel one under a Panel-chosen name.
    private const string SiteNameKey = "platform-setting:SiteName";
    private const string SiteUrlKey = "platform-setting:SiteUrl";

    private static readonly SiteBrandingDto Fallback = new() { SiteName = "RustArchon", SiteUrl = "https://www.rustarchon.com" };

    /// <summary>
    /// The current site name and URL. Falls back to the anonymous Api endpoint if either half isn't in
    /// Valkey yet (see this class's own remarks), and finally to a hardcoded default if that call
    /// itself fails - a nav bar that cannot show its own brand is a worse failure than one showing a
    /// generic placeholder for as long as both the cache and the Api are unreachable.
    /// </summary>
    public async Task<SiteBrandingDto> GetAsync()
    {
        var siteName = await cache.GetStringAsync(SiteNameKey);
        var siteUrl = await cache.GetStringAsync(SiteUrlKey);

        if (!string.IsNullOrEmpty(siteName) && !string.IsNullOrEmpty(siteUrl))
        {
            return new SiteBrandingDto { SiteName = siteName, SiteUrl = siteUrl };
        }

        try
        {
            return await client.GetAsync();
        }
        catch
        {
            return Fallback;
        }
    }
}
