// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using JumpStart.Services.Authentication;
using Microsoft.AspNetCore.SignalR.Client;
using RustArchon.Messaging.Contracts;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// A scoped wrapper around a <see cref="HubConnection"/> to RustArchon.Api's <c>RconHub</c>, for one
/// server's live console/chat/status tail.
/// </summary>
/// <remarks>
/// Callers must ensure <see cref="ITokenStore"/> already holds a real token before calling
/// <see cref="ConnectAsync"/> - the access token provider below reads it synchronously at connection
/// time, not lazily per-request. In practice this is already guaranteed: every page that uses this
/// class calls <c>IRustServerApiClient.GetByIdAsync</c> first (to load the server), which runs
/// through <c>JwtExchangeHandler</c> and populates the store as a side effect.
/// </remarks>
public class RconHubClient : IAsyncDisposable
{
    private readonly ITokenStore _tokenStore;
    private readonly string _apiBaseUrl;
    private HubConnection? _connection;

    public event Action<RconEventDto>? EventReceived;
    public event Action<Guid, RconConnectionStatus, string?>? StatusChanged;

    /// <summary>
    /// Fires for every Logs-tab entry - both a <see cref="RconConnectionStatus"/> transition (also
    /// reported separately via <see cref="StatusChanged"/> for the live badge) and a worker-side
    /// diagnostic that isn't one (<paramref name="status"/> <c>null</c>) - see
    /// <c>WorkerDiagnosticLoggedConsumer</c>'s remarks for why these share one relay.
    /// </summary>
    public event Action<Guid, ConnectionLogLevel, string, RconConnectionStatus?, DateTimeOffset>? LogEntryReceived;

    public event Action<PlayerSessionDto>? PlayerConnected;
    public event Action<string>? PlayerDisconnected;
    public event Action<PlayerSessionDto>? PlayerGeolocated;
    public event Action<PlayerKillEventDto>? PlayerKilled;

    public RconHubClient(ITokenStore tokenStore, IConfiguration configuration)
    {
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7130";
    }

    public async Task ConnectAsync(Guid serverId)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/rcon", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenStore.GetToken());
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<RconEventDto>("ReceiveEvent", dto => EventReceived?.Invoke(dto));
        _connection.On<Guid, RconConnectionStatus, string?>(
            "ReceiveStatusChanged", (id, status, detail) => StatusChanged?.Invoke(id, status, detail));
        _connection.On<Guid, ConnectionLogLevel, string, RconConnectionStatus?, DateTimeOffset>(
            "ReceiveLogEntry", (id, level, message, status, occurredAtUtc) =>
                LogEntryReceived?.Invoke(id, level, message, status, occurredAtUtc));
        _connection.On<PlayerSessionDto>("ReceivePlayerConnected", dto => PlayerConnected?.Invoke(dto));
        _connection.On<string>("ReceivePlayerDisconnected", steamId => PlayerDisconnected?.Invoke(steamId));
        _connection.On<PlayerSessionDto>("ReceivePlayerGeolocated", dto => PlayerGeolocated?.Invoke(dto));
        _connection.On<PlayerKillEventDto>("ReceivePlayerKilled", dto => PlayerKilled?.Invoke(dto));

        // Re-join the group on every (re)connect, including automatic-reconnect - SignalR groups
        // don't survive a dropped connection, even a briefly-reconnected one.
        _connection.Reconnected += _ => _connection.InvokeAsync("JoinServerGroup", serverId);

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinServerGroup", serverId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
