// Copyright ©2026 Scott Blomfield

using JumpStart.Api.Clients;
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
/// mirroring JumpStart's own <c>IProductApiClient</c>.
/// </remarks>
public interface IRustServerApiClient : IApiClient<RustServerDto, CreateRustServerDto, UpdateRustServerDto>
{
}
