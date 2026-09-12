// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>CommunicationsController</c> - the permanent record of outbound email, gated
/// by the same <c>Platform.ManageOrganizations</c> permission as <see cref="INoteApiClient"/> and used
/// from the same two admin screens.
/// </summary>
public interface ICommunicationApiClient
{
    /// <summary>Communications about an Organization, a member, or both.</summary>
    [Get("/")]
    Task<List<CommunicationSummaryDto>> ListAsync(Guid? tenantId, Guid? userId);

    /// <summary>One communication in full, including the body actually sent.</summary>
    [Get("/{id}")]
    Task<CommunicationDetailDto> GetAsync(Guid id);

    /// <summary>Withdraws a communication that hasn't sent yet - refused once it's left Queued.</summary>
    [Post("/{id}/cancel")]
    Task<CommunicationDetailDto> CancelAsync(Guid id);
}
