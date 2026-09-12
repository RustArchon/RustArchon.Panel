// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;

namespace RustArchon.Panel.Infrastructure;

/// <summary>
/// Read-only access to Valkey - the same cache <c>RustArchon.Api</c>'s own <c>IPlatformSettingsCache</c>
/// writes through to on every admin save, reachable from the Panel because both processes sit on the
/// same Docker network and share the one Valkey container (see <c>docker-compose.yml</c>).
/// </summary>
/// <remarks>
/// <para>
/// The Panel never writes here - it has no ownership of what any key means, only the Api does, through
/// its own write path. Reading the exact same cache instead of keeping a second, Panel-local one is
/// what makes a saved change visible everywhere immediately: there is nothing of the Panel's own to
/// invalidate, because there is nothing of the Panel's own being cached.
/// </para>
/// <para>
/// General-purpose by design, not written narrowly for <c>SiteBrandingService</c> - the intent is that
/// the next thing the Panel needs to read from this same cache reaches for this interface rather than
/// standing up its own.
/// </para>
/// </remarks>
public interface IValkeyCache
{
    /// <summary>
    /// Reads a raw string value by its exact key (e.g. <c>"platform-setting:SiteName"</c> - the caller
    /// owns knowing the key format the writer actually used).
    /// </summary>
    /// <returns>
    /// <c>null</c> if Valkey isn't configured, isn't currently reachable, or the key simply isn't
    /// cached (e.g. nothing has read it through the Api since Valkey was last restarted - it has no
    /// durable volume, see <c>docker-compose.yml</c>'s own remarks). A caller needs its own fallback
    /// for this case; there is no durable store behind this interface to fall back to.
    /// </returns>
    Task<string?> GetStringAsync(string key);
}
