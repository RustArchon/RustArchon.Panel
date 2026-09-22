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
    /// What superseding a plan with these terms would do to its current subscribers if they were moved along (who could be, who would stay and why) -
    /// asked before superseding. Nothing is created.
    /// </summary>
    [Post("/{id}/supersede/preview")]
    Task<PlanMovePreviewDto> PreviewSupersedeAsync(Guid id, [Body] SupersedePlanDto terms);

    /// <summary>Who an announcement to this plan's organizations would reach for these criteria. Nothing is sent.</summary>
    [Post("/{id}/announcements/preview")]
    Task<AnnouncementPreviewDto> PreviewAnnouncementAsync(Guid id, [Body] AnnouncementCriteriaDto criteria);

    /// <summary>Emails each language's version of the message to one address, to see how it will look.</summary>
    [Post("/{id}/announcements/test")]
    Task<AnnouncementResultDto> SendAnnouncementTestAsync(Guid id, [Body] SendAnnouncementTestDto request);

    /// <summary>Emails the message to every organization the criteria reach - only if that is the number the admin confirmed (otherwise 409, nothing sent).</summary>
    [Post("/{id}/announcements/send")]
    Task<AnnouncementResultDto> SendAnnouncementAsync(Guid id, [Body] SendPlanAnnouncementDto request);

    /// <summary>The announcements already sent from this plan, newest first.</summary>
    [Get("/{id}/announcements")]
    Task<List<AnnouncementBatchDto>> GetAnnouncementHistoryAsync(Guid id);

    /// <summary>What moving a replaced plan's current subscribers to its latest version would do.</summary>
    [Get("/{id}/move-preview")]
    Task<PlanMovePreviewDto> GetMovePreviewAsync(Guid id);

    /// <summary>Moves a replaced plan's current subscribers to its latest version - those it is no worse for; the rest stay.</summary>
    [Post("/{id}/move-subscribers")]
    Task<PlanMoveResultDto> MoveSubscribersAsync(Guid id);

    /// <summary>Switches a Plan on or off and nothing else - see <see cref="SetPlanActiveDto"/>.</summary>
    [Put("/{id}/active")]
    Task<PlanDto> SetActiveAsync(Guid id, [Body] SetPlanActiveDto dto);

    /// <summary>
    /// Supersedes a Plan that already has subscribers - see <see cref="SupersedePlanDto"/>'s remarks.
    /// </summary>
    [Post("/{id}/supersede")]
    Task<PlanDto> SupersedeAsync(Guid id, [Body] SupersedePlanDto supersedeDto);
}
