// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Messaging.Contracts;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>OrganizationsController</c> - the site-admin view of individual customers,
/// gated server-side by the <c>ManageOrganizations</c> permission.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ISubscriptionApiClient"/>, which answers for the signed-in user's own
/// Organization and nobody else's. This one names the Organization it is asking about, which is the
/// whole difference: it is the only client in the Panel that reaches across the tenant boundary at an
/// individual-account level.
/// </remarks>
public interface IOrganizationApiClient
{
    /// <summary>Organizations matching the filter, newest sign-up first.</summary>
    [Get("/")]
    Task<List<OrganizationSummaryDto>> ListAsync(
        string? search = null,
        Guid? planId = null,
        SubscriptionStatus? status = null,
        bool includeCancelled = false,
        decimal minimumOutstanding = 0m);

    /// <summary>Everything behind one Organization.</summary>
    [Get("/{tenantId}")]
    Task<OrganizationDetailDto> GetAsync(Guid tenantId);

    /// <summary>The plans available to filter by.</summary>
    [Get("/plan-options")]
    Task<List<ReportFilterOptionDto>> GetPlanOptionsAsync();

    /// <summary>Enables one of the Organization's servers.</summary>
    [Post("/{tenantId}/servers/{serverId}/enable")]
    Task EnableServerAsync(Guid tenantId, Guid serverId);

    /// <summary>Disables one of the Organization's servers.</summary>
    [Post("/{tenantId}/servers/{serverId}/disable")]
    Task DisableServerAsync(Guid tenantId, Guid serverId);

    /// <summary>Corrects a server's host, port or RCON password.</summary>
    [Put("/{tenantId}/servers/{serverId}/connection")]
    Task UpdateServerConnectionAsync(
        Guid tenantId, Guid serverId, [Body] UpdateServerConnectionRequestDto request);

    /// <summary>Sends an RCON command to one of the Organization's servers.</summary>
    [Post("/{tenantId}/servers/{serverId}/command")]
    Task<RconCommandResult> SendServerCommandAsync(
        Guid tenantId, Guid serverId, [Body] SendServerCommandRequestDto request);

    /// <summary>Adds somebody to the Organization.</summary>
    [Post("/{tenantId}/members")]
    Task AddMemberAsync(Guid tenantId, [Body] AddMemberRequestDto request);

    /// <summary>Removes somebody from the Organization.</summary>
    [Delete("/{tenantId}/members/{userId}")]
    Task RemoveMemberAsync(Guid tenantId, Guid userId);

    /// <summary>Suspends or restores one person's access without removing them.</summary>
    [Post("/{tenantId}/members/{userId}/active")]
    Task SetMemberActiveAsync(Guid tenantId, Guid userId, bool active);

    /// <summary>Grants one of the Organization's roles.</summary>
    [Post("/{tenantId}/members/{userId}/roles/{roleId}")]
    Task AssignRoleAsync(Guid tenantId, Guid userId, Guid roleId);

    /// <summary>Takes one of the Organization's roles back.</summary>
    [Delete("/{tenantId}/members/{userId}/roles/{roleId}")]
    Task UnassignRoleAsync(Guid tenantId, Guid userId, Guid roleId);

    /// <summary>Moves the Organization between Active, PastDue and Suspended.</summary>
    [Post("/{tenantId}/status")]
    Task SetStatusAsync(Guid tenantId, [Body] SetOrganizationStatusRequestDto request);

    /// <summary>Ends the Organization.</summary>
    [Post("/{tenantId}/cancel")]
    Task CancelAsync(Guid tenantId, [Body] CancelOrganizationRequestDto request);

    /// <summary>Brings a cancelled Organization back.</summary>
    [Post("/{tenantId}/reopen")]
    Task ReopenAsync(Guid tenantId, [Body] ReopenOrganizationRequestDto request);
}

/// <summary>
/// Refit client for <c>PlatformUsersController</c> - the platform-wide directory and the Site Admin
/// role.
/// </summary>
/// <remarks>
/// Answers in user ids only; the Panel merges these with its own Identity store to produce something a
/// human can read. See <c>IAccessAdminService</c> for why the two halves live apart.
/// </remarks>
public interface IPlatformUserApiClient
{
    /// <summary>Every user the Api knows about, with their Organizations and platform role.</summary>
    [Get("/")]
    Task<List<PlatformUserDto>> GetDirectoryAsync();

    /// <summary>Grants the platform-wide "Site Admin" role.</summary>
    [Post("/{userId}/site-admin")]
    Task GrantSiteAdminAsync(Guid userId);

    /// <summary>Takes it away. Refused for the last holder.</summary>
    [Delete("/{userId}/site-admin")]
    Task RevokeSiteAdminAsync(Guid userId);
}
