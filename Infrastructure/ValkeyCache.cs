// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RustArchon.Panel.Infrastructure;

/// <inheritdoc cref="IValkeyCache" />
/// <remarks>
/// Mirrors the degrade-gracefully shape of <c>RustArchon.Api</c>'s own <c>PlatformSettingsCache</c>:
/// <see cref="IConnectionMultiplexer"/> is resolved lazily through <see cref="IServiceProvider"/> so a
/// deployment that never configures <c>Valkey:ConnectionString</c> doesn't turn depending on this into
/// a DI resolution failure, and every Redis operation is wrapped in try/catch so a connectivity blip
/// degrades to "nothing cached" for the caller rather than surfacing as an error.
/// </remarks>
public class ValkeyCache(IServiceProvider serviceProvider, ILogger<ValkeyCache> logger) : IValkeyCache
{
    private IConnectionMultiplexer? Redis => serviceProvider.GetService<IConnectionMultiplexer>();

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key)
    {
        var redis = Redis;
        if (redis is null)
        {
            return null;
        }

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Failed to read '{Key}' from Valkey.", key);
            return null;
        }
    }
}
