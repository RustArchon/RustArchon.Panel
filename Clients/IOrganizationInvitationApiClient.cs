// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>OrganizationInvitationsController</c> - inviting people into the signed-in
/// member's own Organization.
/// </summary>
/// <remarks>
/// Nothing here names a tenant, for the same reason as <see cref="IOrganizationMemberApiClient"/>:
/// the Api takes it from the caller's token, so there is nowhere in the request to say whose
/// Organization to invite somebody into.
/// </remarks>
public interface IOrganizationInvitationApiClient
{
    /// <summary>Invitations sent but not yet taken up.</summary>
    [Get("/")]
    Task<List<OrganizationInvitationDto>> ListAsync();

    /// <summary>Invites somebody, and sends them the link.</summary>
    [Post("/")]
    Task<OrganizationInvitationDto> InviteAsync([Body] InviteMemberRequestDto request);

    /// <summary>Withdraws an invitation, so its link stops working.</summary>
    [Delete("/{invitationId}")]
    Task RevokeAsync(Guid invitationId);
}

/// <summary>
/// Reads an invitation from its token, without being signed in.
/// </summary>
/// <remarks>
/// Registered with no JWT handlers, deliberately. Somebody following an invitation link may have no
/// account at all yet, and attaching the token-exchange handler would try to mint a credential for
/// nobody. Split from <see cref="IInvitationAcceptApiClient"/> rather than sharing one client with a
/// conditional handler, because "this call is anonymous and that one is not" is clearer as two types
/// than as a branch inside a message handler.
/// </remarks>
public interface IInvitationPreviewApiClient
{
    /// <summary>Who is inviting whom, or a 404 if the token names nothing.</summary>
    [Get("/{token}")]
    Task<InvitationPreviewDto> PeekAsync(string token);
}

/// <summary>Accepts an invitation as the signed-in user.</summary>
public interface IInvitationAcceptApiClient
{
    /// <summary>
    /// Redeems the invitation. Always succeeds at the HTTP level - the result says what happened.
    /// </summary>
    [Post("/{token}/accept")]
    Task<AcceptInvitationResultDto> AcceptAsync(string token);
}
