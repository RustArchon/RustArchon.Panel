// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the platform-admin <c>PluginAdminController</c>: rotating and revoking the RustArchon plugin's
/// signing key, and uploading, publishing and withdrawing plugin releases. Only an account holding the Site Admin
/// role's <c>Platform.ManageSettings</c> permission can use it - every call is a <c>403</c> otherwise. A refused action
/// is a <c>400</c> whose body is a plain sentence; <see cref="Services.ApiErrorMessage"/> shows it as is.
/// </summary>
public interface IPluginAdminApiClient
{
    [Get("/keys")]
    Task<List<PluginKeyDto>> GetKeysAsync();

    [Post("/keys/rotate")]
    Task<PluginKeyDto> RotateKeyAsync([Body] RotatePluginKeyRequestDto request);

    [Post("/keys/{fingerprint}/revoke")]
    Task<PluginKeyDto> RevokeKeyAsync(string fingerprint, [Body] RevokePluginKeyRequestDto request);

    [Get("/releases")]
    Task<PluginReleasesDto> GetReleasesAsync();

    /// <summary>Uploads a plugin source file as a draft. <paramref name="kind"/> is <c>main</c> or <c>updater</c>.</summary>
    [Multipart]
    [Post("/releases")]
    Task<PluginReleaseDto> UploadReleaseAsync(
        [AliasAs("kind")] string kind, [AliasAs("notes")] string? notes, [AliasAs("file")] StreamPart file);

    [Post("/releases/{id}/publish")]
    Task<PluginReleaseDto> PublishReleaseAsync(Guid id);

    [Post("/releases/{id}/withdraw")]
    Task<PluginReleaseDto> WithdrawReleaseAsync(Guid id, [Body] WithdrawPluginReleaseRequestDto request);

    [Get("/events")]
    Task<List<PluginAdminEventDto>> GetEventsAsync([Query] int limit = 50);
}
