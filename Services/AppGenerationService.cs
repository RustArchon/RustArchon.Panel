// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using RustArchon.Panel.Infrastructure;

namespace RustArchon.Panel.Services;

/// <summary>
/// Reads the "has something every open circuit is rendering just changed" signal
/// <c>RustArchon.Api</c>'s own <c>IAppGenerationCache</c> writes - straight from the same Valkey cache,
/// not a second, Panel-local one of its own, the same relationship <see cref="SiteBrandingService"/> has
/// to <c>PlatformSettingsCache</c>.
/// </summary>
/// <remarks>
/// A <c>null</c> return means "can't tell" (Valkey unconfigured or unreachable), not "unchanged" - see
/// <see cref="IValkeyCache"/>'s own degrade-gracefully shape. A caller comparing generations must treat
/// <c>null</c> as "don't force a reload", the same posture <c>SiteBrandingService</c> takes toward a
/// cache miss: absence of information is never itself a signal to act on.
/// </remarks>
public class AppGenerationService(IValkeyCache cache)
{
    // Must match RustArchon.Api's AppGenerationCache.Key exactly - this reads the same entry that
    // class writes, not a parallel one under a Panel-chosen name.
    private const string Key = "app-generation";

    public Task<string?> GetCurrentAsync() => cache.GetStringAsync(Key);
}
