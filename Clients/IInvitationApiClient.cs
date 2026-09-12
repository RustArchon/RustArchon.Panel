// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the anonymous invitation endpoints, used by <c>Register.razor</c> before an
/// account exists - registered with no JWT handlers attached (see <c>Program.cs</c>), unlike every
/// other API client in this project, since there's no authenticated user yet to attach a token for.
/// </summary>
public interface IInvitationApiClient
{
    [Get("/status")]
    Task<InvitationStatusDto> GetStatusAsync();

    /// <summary>
    /// Whether this code would redeem, without consuming it - so a bad code is refused before an
    /// account is created, and a good one is not spent until the registration has actually finished.
    /// </summary>
    [Post("/validate")]
    Task<RedeemInvitationCodeResult> ValidateAsync([Body] RedeemInvitationCodeRequest request);

    [Post("/redeem")]
    Task<RedeemInvitationCodeResult> RedeemAsync([Body] RedeemInvitationCodeRequest request);
}
