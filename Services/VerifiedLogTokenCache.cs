// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RustArchon.Panel.Services;

/// <summary>
/// Remembers, for a while and only in memory, which tokens the Api has just accepted, so that if the Api later cannot be reached the Panel can still
/// answer from its own logs for someone who was verified minutes ago. Used <em>only</em> when the Api is unreachable: while it answers, every request is
/// checked live, so a revoked token is refused at once.
/// </summary>
/// <remarks>
/// Without this the diagnostic endpoint would be unusable in exactly the situation it is most wanted (the Api is down and the Panel's log says why).
/// The cost is bounded and spelled out: a token revoked while the Api is down is honoured for at most <see cref="Lifetime"/> afterwards, and only for the
/// Panel's own entries. Only a SHA-256 of the token is held, never the token.
/// </remarks>
public sealed class VerifiedLogTokenCache(TimeProvider clock)
{
    /// <summary>How long an accepted token is remembered for the unreachable-Api case.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public const int MaxEntries = 16;

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _expiry = new(StringComparer.Ordinal);

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _expiry.Count;
            }
        }
    }

    public void Remember(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        lock (_gate)
        {
            var now = clock.GetUtcNow();
            foreach (var expired in _expiry.Where(p => p.Value <= now).Select(p => p.Key).ToList())
            {
                _expiry.Remove(expired);
            }

            _expiry[Hash(token)] = now + Lifetime;
            while (_expiry.Count > MaxEntries)
            {
                _expiry.Remove(_expiry.MinBy(p => p.Value).Key);
            }
        }
    }

    public bool Contains(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        lock (_gate)
        {
            return _expiry.TryGetValue(Hash(token), out var until) && until > clock.GetUtcNow();
        }
    }

    public void Forget(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        lock (_gate)
        {
            _expiry.Remove(Hash(token));
        }
    }

    /// <summary>What is held, for a test to check it never contains a token.</summary>
    public string DebugView()
    {
        lock (_gate)
        {
            return string.Join(';', _expiry.Select(p => p.Key + "=" + p.Value.ToString("O")));
        }
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
