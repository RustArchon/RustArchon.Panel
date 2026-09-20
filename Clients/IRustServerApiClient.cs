// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using JumpStart.Api.Clients;
using JumpStart.Repositories;
using Refit;
using RustArchon.Messaging.Contracts;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for <c>RustServersController</c> endpoints.
/// </summary>
/// <remarks>
/// Registered manually in <c>Program.cs</c> rather than via <c>[ApiClientFor&lt;...&gt;]</c> auto-
/// discovery - that attribute requires the entity and repository types as generic arguments, which
/// would force this Blazor project to reference <c>RustServer</c>/<c>IRustServerRepository</c> from
/// RustArchon.Api. This project reaches every RustArchon entity only through DTOs and API clients,
/// mirroring JumpStart's own <c>IProductApiClient</c>. Routes below are relative to the client's own
/// registered base address (<c>{apiBaseUrl}/api/rustservers</c> - see <c>Program.cs</c>), matching
/// the inherited CRUD methods' own <c>/{id}</c>-style routes.
/// </remarks>
public interface IRustServerApiClient : IApiClient<RustServerDto, CreateRustServerDto, UpdateRustServerDto>
{
    [Post("/{id}/enable")]
    Task EnableAsync(Guid id);

    [Post("/{id}/disable")]
    Task DisableAsync(Guid id);

    [Post("/{id}/command")]
    Task<RconCommandResult> SendCommandAsync(Guid id, [Body] SendCommandRequest request);

    [Get("/{id}/events")]
    Task<PagedResult<RconEventDto>> GetEventsAsync(
        Guid id,
        [Query] int pageNumber = 1,
        [Query] int pageSize = 100,
        [Query] bool? isChat = null,
        [Query] DateTimeOffset? since = null,
        [Query] DateTimeOffset? until = null,
        [Query] bool includeNonInteractive = false);

    [Get("/{id}/players")]
    Task<List<PlayerSessionDto>> GetCurrentPlayersAsync(Guid id);

    [Get("/{id}/players/history")]
    Task<PagedResult<PlayerSessionDto>> GetPlayerHistoryAsync(
        Guid id,
        [Query] int pageNumber = 1,
        [Query] int pageSize = 100,
        [Query] DateTimeOffset? since = null,
        [Query] DateTimeOffset? until = null);

    [Get("/{id}/kills")]
    Task<PagedResult<PlayerKillEventDto>> GetKillsAsync(
        Guid id,
        [Query] int pageNumber = 1,
        [Query] int pageSize = 100,
        [Query] DateTimeOffset? since = null,
        [Query] DateTimeOffset? until = null);

    [Get("/{id}/players/inactive")]
    Task<PagedResult<InactivePlayerDto>> GetInactivePlayersAsync(
        Guid id, [Query] int pageNumber = 1, [Query] int pageSize = 100);

    [Get("/{id}/players/{steamId}")]
    Task<PlayerDetailDto> GetPlayerDetailAsync(Guid id, string steamId);

    [Get("/{id}/players/{steamId}/sessions")]
    Task<PagedResult<PlayerSessionDto>> GetPlayerSessionsAsync(
        Guid id, string steamId, [Query] int pageNumber = 1, [Query] int pageSize = 100);

    [Get("/{id}/players/{steamId}/kills")]
    Task<PagedResult<PlayerKillEventDto>> GetPlayerKillsAsync(
        Guid id, string steamId, [Query] int pageNumber = 1, [Query] int pageSize = 100);

    /// <summary>
    /// The Stats tab's graph data (player count, network in/out, memory), oldest first. Defaults to
    /// the last 24 hours when <paramref name="since"/> is omitted - see the endpoint's own remarks.
    /// </summary>
    [Get("/{id}/serverinfo/history")]
    Task<List<ServerInfoSnapshotDto>> GetServerInfoHistoryAsync(
        Guid id,
        [Query] DateTimeOffset? since = null,
        [Query] DateTimeOffset? until = null);

    /// <summary>
    /// The Stats tab's connection log - WebRCON connection-status transitions, newest first. Defaults
    /// to the last 24 hours when <paramref name="since"/> is omitted - see the endpoint's own remarks.
    /// </summary>
    [Get("/{id}/connection-log")]
    Task<List<ConnectionLogEntryDto>> GetConnectionLogAsync(
        Guid id,
        [Query] DateTimeOffset? since = null,
        [Query] DateTimeOffset? until = null);

    /// <summary>
    /// The Plugins tab's list - the Oxide/Carbon plugins the Worker last reported loaded on this server,
    /// ordered by name. Empty when there's no plugin framework, nothing loaded, or no poll has landed yet.
    /// </summary>
    [Get("/{id}/plugins")]
    Task<List<ServerPluginDto>> GetPluginsAsync(Guid id);

    /// <summary>
    /// The installed plugins that UpdateChecker (a third-party plugin on the game server) says have a newer version, with everything it
    /// reported. Only notices that still hold are returned. Empty when the server does not run UpdateChecker, has the RustArchon
    /// plugin without the updates capability, or every plugin is current.
    /// </summary>
    [Get("/{id}/plugin-updates")]
    Task<List<PluginUpdateNoticeDto>> GetPluginUpdatesAsync(Guid id);

    /// <summary>
    /// The plugin files Carbon says failed to load on this server, and why, by file then position. Empty when nothing failed, the server does not
    /// run Carbon, or no plugin-list poll has landed yet. The text came from the game server.
    /// </summary>
    [Get("/{id}/plugin-failures")]
    Task<List<ServerPluginFailureDto>> GetPluginFailuresAsync(Guid id);

    /// <summary>
    /// The recent times the Panel asked this server to update its plugin or Updater (by a click or automatically) and how each turned out,
    /// newest first.
    /// </summary>
    [Get("/{id}/plugin-update-attempts")]
    Task<List<PluginUpdateAttemptDto>> GetPluginUpdateAttemptsAsync(Guid id);

    /// <summary>
    /// What the optional RustArchon companion plugin last reported on this server. <c>204 No Content</c> (a null
    /// <c>Content</c>) is the ordinary answer when the plugin is not installed or has not answered yet, which is
    /// why this returns the raw response rather than throwing on it. Only trust it while the plugin is also in
    /// <see cref="GetPluginsAsync"/>'s list: it can be stale after an uninstall.
    /// </summary>
    [Get("/{id}/plugin-status")]
    Task<IApiResponse<ServerPluginStatusDto>> GetPluginStatusAsync(Guid id);

    /// <summary>
    /// The RustArchon plugin script, signed by this deployment - what an admin uploads to the game server. Returns
    /// the raw response (not throwing on an error status) so the caller can check <c>IsSuccessStatusCode</c> and read
    /// the bytes exactly as sent: the signature covers them, so they must not pass through anything that re-encodes.
    /// </summary>
    [Get("/plugin/download")]
    Task<HttpResponseMessage> DownloadPluginAsync();

    /// <summary>
    /// The server's combat log from the RustArchon plugin, newest first. <paramref name="playerId"/> is a SteamID64;
    /// <paramref name="since"/> and <paramref name="until"/> bound the time window; <paramref name="limit"/> is 1 to 500.
    /// </summary>
    [Get("/{id}/combat")]
    Task<CombatLogDto> GetCombatLogAsync(
        Guid id, [Query] DateTimeOffset? since = null, [Query] DateTimeOffset? until = null,
        [Query] string? playerId = null, [Query] int limit = 100);

    /// <summary>
    /// The server's tool cupboards (bases) from the RustArchon plugin. Needs the <c>RustServer.ViewBases</c> permission:
    /// without it the Api answers <c>403</c>, which the caller shows as "not allowed", not as an error.
    /// </summary>
    [Get("/{id}/bases")]
    Task<BasesDto> GetBasesAsync(Guid id);

    /// <summary>
    /// What the Panel knows about the server's current world map: its size, seed and named places, and whether the picture has
    /// arrived. Needs only ordinary access to the server (the map is the game's own public map).
    /// </summary>
    [Get("/{id}/map")]
    Task<MapDto> GetMapAsync(Guid id);

    /// <summary>
    /// The map picture, as the raw response so the caller can stream it (it is tens of megabytes) instead of buffering it here.
    /// </summary>
    [Get("/{id}/map/image")]
    Task<HttpResponseMessage> GetMapImageAsync(Guid id);

    /// <summary>
    /// Where players were and are, from the positions the RustArchon plugin records, newest first. Needs the
    /// <c>RustServer.ViewPositions</c> permission: without it the Api answers <c>403</c>, which the caller shows as "not
    /// allowed", not as an error. <paramref name="playerId"/> is a SteamID64; <paramref name="limit"/> is 1 to 5000.
    /// </summary>
    [Get("/{id}/positions")]
    Task<PositionsDto> GetPositionsAsync(
        Guid id, [Query] DateTimeOffset? since = null, [Query] DateTimeOffset? until = null,
        [Query] string? playerId = null, [Query] int limit = 500);

    /// <summary>
    /// The signed Updater plugin - the small second plugin an admin installs once so later RustArchon updates can be
    /// applied from the Panel. Raw response for the same reason as <see cref="DownloadPluginAsync"/>.
    /// </summary>
    [Get("/plugin/download-updater")]
    Task<HttpResponseMessage> DownloadPluginUpdaterAsync();

    /// <summary>
    /// Asks the server's Updater to install the version this Panel serves. Every refusal is an ordinary result with a
    /// <see cref="PluginUpdateResultDto.Code"/> saying why, not an HTTP error.
    /// </summary>
    [Post("/{id}/plugin/update")]
    Task<PluginUpdateResultDto> StartPluginUpdateAsync(Guid id);

    /// <summary>
    /// Asks the server's RustArchon plugin to install, or update, the Updater plugin (the Updater cannot replace itself). Same result
    /// shape as <see cref="StartPluginUpdateAsync"/>: every refusal is an ordinary result with a code.
    /// </summary>
    [Post("/{id}/plugin/update-updater")]
    Task<PluginUpdateResultDto> StartUpdaterUpdateAsync(Guid id);

    /// <summary>Records that the Add Server wizard was finished for this server. Idempotent.</summary>
    [Post("/{id}/setup-complete")]
    Task<RustServerDto> CompleteSetupAsync(Guid id);

    /// <summary>
    /// Saves the plugin's Recording and Combat log switches for this server. A separate call from the full-record
    /// <c>UpdateAsync</c> on purpose - see <see cref="UpdateServerPluginSettingsDto"/>.
    /// </summary>
    [Put("/{id}/plugin-settings")]
    Task<RustServerDto> UpdatePluginSettingsAsync(Guid id, [Body] UpdateServerPluginSettingsDto settings);

    /// <summary>
    /// The calling tenant's current Plan limits and server count - lets the Servers page warn "you're
    /// at your limit" the moment a user clicks Add Server, before filling out the form. See the
    /// endpoint's own remarks for why this can't replace <c>CreateAsync</c>'s own rejection.
    /// </summary>
    [Get("/plan-limit")]
    Task<ServerPlanLimitDto> GetPlanLimitAsync();

    /// <summary>
    /// A server's in-game (F7) reports, newest first. Needs <c>RustServer.ViewReports</c>: without it the Api answers <c>403</c>,
    /// which the caller shows as "not allowed", not as an error.
    /// </summary>
    [Get("/{id}/reports")]
    Task<ServerReportListDto> GetReportsAsync(
        Guid id,
        [Query] int pageNumber = 1,
        [Query] int pageSize = 25,
        [Query] ServerReportType? type = null,
        [Query] ServerReportStatus? status = null,
        [Query] string? targetSteamId = null);

    /// <summary>How many of the server's reports are still new, for the tab's badge. Same permission as the list.</summary>
    [Get("/{id}/reports/count")]
    Task<ServerReportCountDto> GetReportCountAsync(Guid id);

    /// <summary>A report's screenshot as the raw response, so the caller can hand it to the browser without buffering it here.</summary>
    [Get("/{id}/reports/{reportId}/screenshot")]
    Task<HttpResponseMessage> GetReportScreenshotAsync(Guid id, Guid reportId);

    /// <summary>Changes what has been done about a report. Needs <c>RustServer.ManageReports</c> as well as the view permission.</summary>
    [Put("/{id}/reports/{reportId}/status")]
    Task<ServerReportDto> SetReportStatusAsync(Guid id, Guid reportId, [Body] UpdateServerReportStatusDto status);

    /// <summary>
    /// The address to paste into the game server's <c>server.reportsServerEndpoint</c>. The first ask mints the secret in it. Needs
    /// <c>RustServer.ManageReportForwarding</c> (<c>403</c> otherwise), because the address contains that secret.
    /// </summary>
    [Get("/{id}/report-forwarding")]
    Task<ReportForwardingDto> GetReportForwardingAsync(Guid id);

    /// <summary>Replaces the secret; the previous address stops working immediately.</summary>
    [Post("/{id}/report-forwarding/rotate")]
    Task<ReportForwardingDto> RotateReportForwardingAsync(Guid id);

    /// <summary>Asks the game server what it is set to and compares it to the address.</summary>
    [Post("/{id}/report-forwarding/verify")]
    Task<VerifyReportForwardingResultDto> VerifyReportForwardingAsync(Guid id);
}

/// <summary>
/// Body for <see cref="IRustServerApiClient.SendCommandAsync"/> - mirrors
/// <c>RustServersController.SendCommandRequest</c> exactly (same property names, case-insensitive
/// JSON matching).
/// </summary>
public class SendCommandRequest
{
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Whether a human actually typed/clicked this. No default, deliberately - every call site
    /// constructing this has to set it explicitly, or the build fails; see
    /// <c>RustArchon.Messaging.Contracts.SendRconCommand.Interactive</c>'s remarks for why. A page-load
    /// side effect (a background fetch the Panel makes on its own, not because someone asked for it)
    /// must set this <c>false</c>; a Console/Control-tab command, or a per-player moderation action a
    /// user actually clicked, sets it <c>true</c>.
    /// </summary>
    public required bool Interactive { get; set; }
}
