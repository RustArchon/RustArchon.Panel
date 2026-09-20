// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Net.Http;
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

    /// <summary>Whether the active key has gone long enough without being rotated that a reminder is due. Read-only.</summary>
    [Get("/keys/reminder")]
    Task<PluginKeyReminderDto> GetKeyReminderAsync();

    [Post("/keys/rotate")]
    Task<PluginKeyDto> RotateKeyAsync([Body] RotatePluginKeyRequestDto request);

    [Post("/keys/{fingerprint}/revoke")]
    Task<PluginKeyDto> RevokeKeyAsync(string fingerprint, [Body] RevokePluginKeyRequestDto request);

    /// <summary>
    /// Exports every signing key into a passphrase-protected file. Returned as the raw response so the caller can hand the file to
    /// the browser byte for byte; a refusal is a <c>400</c> whose body is a sentence. The file holds private keys.
    /// </summary>
    [Post("/keys/export")]
    Task<HttpResponseMessage> ExportKeysAsync([Body] ExportPluginKeysRequestDto request);

    /// <summary>Imports an exported bundle, or with <c>DryRun</c> only reports what importing would do.</summary>
    [Post("/keys/import")]
    Task<PluginKeyImportResultDto> ImportKeysAsync([Body] ImportPluginKeysRequestDto request);

    [Get("/releases")]
    Task<PluginReleasesDto> GetReleasesAsync();

    /// <summary>Uploads a plugin source file as a draft. <paramref name="kind"/> is <c>main</c> or <c>updater</c>.</summary>
    [Multipart]
    [Post("/releases")]
    Task<PluginReleaseDto> UploadReleaseAsync(
        [AliasAs("kind")] string kind, [AliasAs("notes")] string? notes, [AliasAs("file")] StreamPart file);

    /// <summary>
    /// Signs a file that will not be published, for a hand install, and returns the signed file as the raw response. A refusal is a <c>400</c>
    /// whose body is a sentence. The note (why) is required and goes into the audit log.
    /// </summary>
    [Multipart]
    [Post("/sign")]
    Task<HttpResponseMessage> SignFileAsync(
        [AliasAs("kind")] string kind, [AliasAs("note")] string note, [AliasAs("file")] StreamPart file);

    /// <summary>A draft's or published release, signed with the active key, as the raw response - to test it before publishing. Audit-logged.</summary>
    [Get("/releases/{id}/download")]
    Task<HttpResponseMessage> DownloadReleaseAsync(Guid id);

    [Post("/releases/{id}/publish")]
    Task<PluginReleaseDto> PublishReleaseAsync(Guid id);

    [Post("/releases/{id}/withdraw")]
    Task<PluginReleaseDto> WithdrawReleaseAsync(Guid id, [Body] WithdrawPluginReleaseRequestDto request);

    [Get("/events")]
    Task<List<PluginAdminEventDto>> GetEventsAsync([Query] int limit = 50);
}
