// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using RustArchon.Messaging.Contracts;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// A scoped connection to RustArchon.Api's <c>RconHub</c>, for one server's live console/chat/status
/// tail. See <see cref="RconHubClient"/> for the real, SignalR-backed implementation.
/// </summary>
/// <remarks>
/// Exists as its own interface purely so a page's live-event handlers (<c>ServerDetail.razor</c>'s
/// <c>OnLogEntryReceived</c> and friends) can be exercised in a test without opening a real SignalR
/// connection - <see cref="RconHubClient.ConnectAsync"/> talks to a real network endpoint with no other
/// seam. Every member here is exactly what <c>RconHubClient</c> already exposed; this changes no
/// behavior, only what a consumer (and a test double) can depend on.
/// </remarks>
public interface IRconHubClient : IAsyncDisposable
{
    event Action<RconEventDto>? EventReceived;
    event Action<Guid, RconConnectionStatus, string?>? StatusChanged;

    /// <summary>
    /// Fires for every Logs-tab entry - both a <see cref="RconConnectionStatus"/> transition (also
    /// reported separately via <see cref="StatusChanged"/> for the live badge) and a worker-side
    /// diagnostic that isn't one (<c>status</c> <c>null</c>).
    /// </summary>
    event Action<Guid, ConnectionLogLevel, string, RconConnectionStatus?, DateTimeOffset>? LogEntryReceived;

    event Action<PlayerSessionDto>? PlayerConnected;
    event Action<string>? PlayerDisconnected;
    event Action<PlayerSessionDto>? PlayerGeolocated;
    event Action<PlayerKillEventDto>? PlayerKilled;

    /// <summary>
    /// Fires when a report arrived on, or changed for, this server. Carries no report - the group it comes down is everyone who may see
    /// the server, wider than everyone who may read its reports - so a listener re-reads through the endpoint that checks the permission.
    /// </summary>
    event Action? ServerReportsChanged;

    Task ConnectAsync(Guid serverId);

    /// <summary>
    /// Switches between this server's ordinary (interactive-only) live group and its unfiltered one.
    /// </summary>
    Task SetUnfilteredAsync(bool unfiltered);
}
