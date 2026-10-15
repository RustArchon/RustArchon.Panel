// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;

namespace RustArchon.Panel.Services;

/// <summary>
/// Counts refused diagnostic-log requests per network address over a sliding minute and says when an address has had its fill. Only
/// <em>refusals</em> count, so a caller with the right token is never slowed; the limit is a Platform Setting (see <see cref="LogReadLimit"/>).
/// </summary>
/// <remarks>
/// In memory and per process, which is the point: this is noise control against someone guessing at the door, not the thing that keeps the token
/// safe (that is its 256 bits). The table of addresses is bounded; if it fills, addresses whose failures have all aged out go first.
/// </remarks>
public sealed class LogReadThrottle(TimeProvider clock, Func<int> failuresPerMinute)
{
    /// <summary>The most addresses remembered at once.</summary>
    public const int MaxAddresses = 10_000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _failures = new(StringComparer.Ordinal);

    public int TrackedAddresses
    {
        get
        {
            lock (_gate)
            {
                return _failures.Count;
            }
        }
    }

    public bool IsBlocked(string address)
    {
        lock (_gate)
        {
            if (!_failures.TryGetValue(address, out var queue))
            {
                return false;
            }

            Prune(queue, clock.GetUtcNow());
            if (queue.Count == 0)
            {
                _failures.Remove(address);
                return false;
            }

            return queue.Count >= Math.Max(1, failuresPerMinute());
        }
    }

    public void RecordFailure(string address)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (!_failures.TryGetValue(address, out var queue))
            {
                if (_failures.Count >= MaxAddresses)
                {
                    MakeRoom(now);
                }

                queue = new Queue<DateTimeOffset>();
                _failures[address] = queue;
            }

            Prune(queue, now);
            queue.Enqueue(now);
        }
    }

    private void MakeRoom(DateTimeOffset now)
    {
        foreach (var address in _failures.Keys.ToList())
        {
            var queue = _failures[address];
            Prune(queue, now);
            if (queue.Count == 0)
            {
                _failures.Remove(address);
            }
        }

        // Still full of addresses that are all misbehaving right now: forget the one that has failed longest ago rather than refuse to count.
        while (_failures.Count >= MaxAddresses)
        {
            var oldest = _failures.MinBy(p => p.Value.Peek()).Key;
            _failures.Remove(oldest);
        }
    }

    private static void Prune(Queue<DateTimeOffset> queue, DateTimeOffset now)
    {
        while (queue.Count > 0 && now - queue.Peek() >= Window)
        {
            queue.Dequeue();
        }
    }
}
