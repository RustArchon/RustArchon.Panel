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

    [Post("/redeem")]
    Task<RedeemInvitationCodeResult> RedeemAsync([Body] RedeemInvitationCodeRequest request);
}
