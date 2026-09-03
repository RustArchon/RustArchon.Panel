// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JumpStart.Api.Clients;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for the platform-admin <c>PlansController</c> endpoints. Only usable by an
/// account holding the <c>ManagePlans</c> permission - every call 403s otherwise. The anonymous
/// equivalent (<c>PublicPlansController</c>) is only ever consumed by RustArchon.Web's pricing page,
/// not this project.
/// </summary>
public interface IPlanApiClient : IApiClient<PlanDto, CreatePlanDto, UpdatePlanDto>
{
    /// <summary>
    /// Every historical Plan row, grouped by Type then newest first. Deliberately separate from the
    /// inherited <see cref="IApiClient{TDto,TCreateDto,TUpdateDto}.GetAllAsync"/> - <c>PlansController</c>
    /// returns a plain <c>List&lt;PlanDto&gt;</c> (every historical row, unpaged) rather than a
    /// <see cref="JumpStart.Repositories.PagedResult{T}"/>, so the inherited method's deserialization
    /// wouldn't match the actual response shape. Same route, distinct method - both are valid Refit
    /// mappings to <c>GET api/plans</c>, only this one is ever called by the admin page.
    /// </summary>
    [Get("")]
    Task<List<PlanDto>> GetAllPlansAsync();

    /// <summary>
    /// Supersedes a Plan that already has subscribers - see <see cref="SupersedePlanDto"/>'s remarks.
    /// </summary>
    [Post("/{id}/supersede")]
    Task<PlanDto> SupersedeAsync(Guid id, [Body] SupersedePlanDto supersedeDto);
}
