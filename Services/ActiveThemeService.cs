// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using RustArchon.Panel.Infrastructure;

namespace RustArchon.Panel.Services;

/// <summary>
/// Reads the platform's currently-active theme id - straight from the same Valkey cache
/// <c>RustArchon.Api</c>'s own <c>IActiveThemeCache</c> writes through to on every activation, not a
/// second, Panel-local cache of its own. Same relationship <see cref="SiteBrandingService"/> has to
/// <c>PlatformSettingsCache</c> and <see cref="AppGenerationService"/> has to <c>IAppGenerationCache</c>.
/// </summary>
/// <remarks>
/// <c>null</c> means "can't tell" (Valkey unconfigured/unreachable, or nothing has activated a theme
/// through this Api yet) - not an error, and not "no theme." A caller treats it as "don't emit the
/// extra stylesheet link this render" and leaves the platform's own baked-in look to apply on its own -
/// see <c>ActiveThemeStylesheetLink.razor</c>'s remarks for why that's always a safe, fully-styled
/// fallback rather than a broken page.
/// </remarks>
public class ActiveThemeService(IValkeyCache cache)
{
    // Must match RustArchon.Api's ActiveThemeCache.Key exactly - this reads the same entry that class
    // writes, not a parallel one under a Panel-chosen name.
    private const string Key = "active-theme-id";

    public async Task<Guid?> GetCurrentAsync()
    {
        var raw = await cache.GetStringAsync(Key);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
