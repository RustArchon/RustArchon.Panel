// Copyright ©2026 Scott Blomfield

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RustArchon.Panel.Services;

/// <summary>
/// How many refused requests one network address may make to the diagnostic log address per minute: a Platform Setting the administrators own and
/// only the Api can read, fetched from it and remembered. Same shape and reasoning as <see cref="ReportIngestLimit"/>: a throttle cannot wait for a
/// network call, so a read answers from the last value seen and refreshes in the background, and the answer is <see cref="Default"/> until the Api
/// has answered (and whenever it cannot be reached), so a limit is never absent because the Api is briefly down.
/// </summary>
public class LogReadLimit(IHttpClientFactory clients, TimeProvider clock, ILogger<LogReadLimit> logger)
{
    /// <summary>What applies until the Api has answered: the Api's own default for the setting.</summary>
    public const int Default = 10;

    /// <summary>How stale the value may get before a read starts a refresh.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(60);

    private int _failuresPerAddressPerMinute = Default;
    private long _refreshedAtTicks;
    private int _refreshing;

    /// <summary>The current limit, never waiting for the refresh it may start.</summary>
    public int FailuresPerAddressPerMinute
    {
        get
        {
            var age = clock.GetUtcNow().UtcTicks - Interlocked.Read(ref _refreshedAtTicks);
            if ((_refreshedAtTicks == 0 || age > MaxAge.Ticks) && Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
            {
                _ = RefreshAsync();
            }

            return Volatile.Read(ref _failuresPerAddressPerMinute);
        }
    }

    /// <summary>Asks the Api now and returns when done. What the background refresh runs; also lets a test wait for it.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            using var response = await clients.CreateClient(LogReadProxy.ClientName).GetAsync("/internal/logs/limits");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<Limits>();
            if (body is { FailuresPerAddressPerMinute: > 0 })
            {
                Volatile.Write(ref _failuresPerAddressPerMinute, body.FailuresPerAddressPerMinute);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read the diagnostic log refusal limit from the Api; keeping {Limit} per minute.", Volatile.Read(ref _failuresPerAddressPerMinute));
        }
        finally
        {
            // Stamped on failure too, so an Api that is down is asked again in MaxAge, not on every request.
            Interlocked.Exchange(ref _refreshedAtTicks, clock.GetUtcNow().UtcTicks);
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private sealed record Limits(int FailuresPerAddressPerMinute);
}
