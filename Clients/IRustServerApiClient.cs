// Copyright ©2026 Scott Blomfield

using System;
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
        [Query] DateTimeOffset? until = null);
}

/// <summary>
/// Body for <see cref="IRustServerApiClient.SendCommandAsync"/> - mirrors
/// <c>RustServersController.SendCommandRequest</c> exactly (same property name, case-insensitive
/// JSON matching).
/// </summary>
public class SendCommandRequest
{
    public string Command { get; set; } = string.Empty;
}
