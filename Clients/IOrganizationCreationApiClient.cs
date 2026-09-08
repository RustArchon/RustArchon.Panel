// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>OrganizationController</c> - creating an Organization of one's own.
/// </summary>
/// <remarks>
/// Every call here is about the caller rather than about an Organization, which is why none of them
/// names one: the Organization does not exist yet, and the two reads are "what could I start one on?"
/// and "what have I started already?".
/// </remarks>
public interface IOrganizationCreationApiClient
{
    /// <summary>Plans a new Organization could start on, with the ones already used marked.</summary>
    [Get("/plan-options")]
    Task<List<NewOrganizationPlanDto>> PlanOptionsAsync();

    /// <summary>Organizations this caller founded.</summary>
    [Get("/founded")]
    Task<List<FoundedOrganizationDto>> FoundedAsync();

    /// <summary>Creates one.</summary>
    [Post("")]
    Task<CreatedOrganizationDto> CreateAsync([Body] CreateOrganizationRequestDto request);
}
