// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the anonymous <c>PublicBrandingController</c> - the platform's own display name
/// and public site URL, readable before anyone is signed in. No JWT handlers, the same reasoning as
/// <see cref="IInvitationPreviewApiClient"/>: the nav bar (and the login page it renders on) needs this
/// on every page, including for a visitor with no account and no token to exchange.
/// </summary>
public interface ISiteBrandingApiClient
{
    [Get("")]
    Task<SiteBrandingDto> GetAsync();
}
