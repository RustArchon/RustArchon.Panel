// Copyright ©2026 Scott Blomfield

using JumpStart.Api.Clients;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for the platform-admin <c>InvitationCodesController</c> endpoints. Only
/// usable by the account matching the API's <c>RUSTARCHON_ADMIN_EMAIL</c> - every call 403s
/// otherwise. See <see cref="IInvitationApiClient"/> for the anonymous redemption side used by
/// Register.razor.
/// </summary>
public interface IInvitationCodeApiClient : IApiClient<InvitationCodeDto, CreateInvitationCodeDto, UpdateInvitationCodeDto>
{
}
