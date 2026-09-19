// Copyright ©2026 Scott Blomfield

using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>GuestTicketController</c> - an anonymous submitter's own access to a single
/// ticket by its guest-access token. No handlers registered (see its own registration in
/// <c>Program.cs</c>) - the person following this link may have no account at all.
/// </summary>
public interface IGuestTicketApiClient
{
    [Get("/{token}")]
    Task<TicketDetailDto> GetAsync(string token);

    [Post("/{token}/messages")]
    Task<TicketMessageDto> AddMessageAsync(string token, [Body] SaveTicketMessageRequestDto request);
}
