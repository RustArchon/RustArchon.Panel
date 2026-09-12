// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>OrganizationRolesController</c> - an Organization defining its own roles.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="IOrganizationApiClient"/> and deliberately unlike it: nothing here
/// names a tenant, because the Api takes it from the caller's token. A member cannot ask about
/// somebody else's roles through this client because there is nowhere in the request to say whose.
/// </remarks>
public interface IOrganizationRoleApiClient
{
    /// <summary>The Organization's roles, and the permissions it may put in them.</summary>
    [Get("/")]
    Task<OrganizationRolesDto> ListAsync();

    /// <summary>Defines a new role.</summary>
    [Post("/")]
    Task<OrganizationRoleDto> CreateAsync([Body] SaveRoleRequestDto request);

    /// <summary>Renames one of the Organization's own roles.</summary>
    [Put("/{roleId}")]
    Task<OrganizationRoleDto> UpdateAsync(Guid roleId, [Body] SaveRoleRequestDto request);

    /// <summary>Retires one of the Organization's own roles.</summary>
    [Delete("/{roleId}")]
    Task DeleteAsync(Guid roleId);

    /// <summary>Replaces what a role grants.</summary>
    [Put("/{roleId}/permissions")]
    Task<OrganizationRoleDto> SetPermissionsAsync(
        Guid roleId, [Body] SetRolePermissionsRequestDto request);
}

/// <summary>
/// Refit client for <c>OrganizationMembersController</c> - an Organization managing its own people.
/// </summary>
/// <remarks>
/// Answers in user ids only; the Panel joins them with its own Identity store to show names. Same
/// tenant-from-the-token rule as <see cref="IOrganizationRoleApiClient"/> above.
/// </remarks>
public interface IOrganizationMemberApiClient
{
    /// <summary>Everyone in the Organization, with the roles they hold.</summary>
    [Get("/")]
    Task<List<OrganizationMemberDto>> ListAsync();

    /// <summary>Grants one of the Organization's roles.</summary>
    [Post("/{userId}/roles/{roleId}")]
    Task AssignRoleAsync(Guid userId, Guid roleId);

    /// <summary>Takes one of the Organization's roles back.</summary>
    [Delete("/{userId}/roles/{roleId}")]
    Task UnassignRoleAsync(Guid userId, Guid roleId);

    /// <summary>Suspends or restores one person's access without removing them.</summary>
    [Post("/{userId}/active")]
    Task SetActiveAsync(Guid userId, bool active);

    /// <summary>Removes somebody from the Organization.</summary>
    [Delete("/{userId}")]
    Task RemoveAsync(Guid userId);
}

/// <summary>
/// Refit client for <c>OrganizationSettingsController</c> - an Organization editing its own name and
/// contact address.
/// </summary>
/// <remarks>Same tenant-from-the-token rule as <see cref="IOrganizationRoleApiClient"/> above.</remarks>
public interface IOrganizationSettingsApiClient
{
    /// <summary>The Organization's own name and contact address.</summary>
    [Get("/")]
    Task<OrganizationSettingsDto> GetAsync();

    /// <summary>Changes them.</summary>
    [Put("/")]
    Task UpdateAsync([Body] UpdateOrganizationSettingsRequestDto request);
}
