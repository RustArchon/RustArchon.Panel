// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for the platform-admin <c>PlatformSettingsController</c> endpoints. Only
/// usable by an account holding the "Site Admin" role's <c>Platform.ManageSettings</c> permission -
/// every call 403s otherwise. Not <see cref="JumpStart.Api.Clients.IApiClient{TDto,TCreateDto,TUpdateDto}"/>-based
/// like <see cref="IInvitationCodeApiClient"/> - that interface's CRUD shape doesn't fit a controller
/// with no Create/Delete and an Update keyed by string rather than <c>Guid</c>; see
/// <c>PlatformSettingsController</c>'s remarks for why.
/// </summary>
public interface IPlatformSettingsApiClient
{
    [Get("")]
    Task<List<PlatformSettingDto>> GetAllAsync();

    [Put("/{key}")]
    Task<PlatformSettingDto> UpdateValueAsync(string key, [Body] UpdatePlatformSettingValueDto request);
}
