// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for the platform-admin <c>ThemesController</c> endpoints. Only usable by an
/// account holding the "Site Admin" role's <c>Platform.ManageSettings</c> permission - every call 403s
/// otherwise, same gate as <see cref="IPlatformSettingsApiClient"/>.
/// </summary>
public interface IThemesApiClient
{
    [Get("")]
    Task<List<ThemeSummaryDto>> GetAllAsync();

    [Get("/{id}")]
    Task<ThemeDetailDto> GetByIdAsync(Guid id);

    /// <summary>Reads one of this theme's own assets back (<c>theme.css</c>, or an image/font under
    /// <paramref name="path"/>) - what the theme-builder's "edit this theme" flow uses to pre-fill the
    /// form with a theme's actual current content. See
    /// <c>RustArchon.Api.Administration.ThemeService.GetAssetAsync</c>.</summary>
    [Get("/{id}/assets/{**path}")]
    Task<Stream> GetAssetAsync(Guid id, string path);

    /// <summary>
    /// Uploads a theme package - just the zip, nothing else: its name/version/author/etc. all come
    /// from its own <c>manifest.json</c>, not a caller-supplied value (see
    /// <c>RustArchon.Api.Administration.ThemeService.UploadAsync</c>'s remarks). <paramref name="package"/>
    /// is disposed by Refit once the request completes - the caller doesn't need to (and shouldn't)
    /// dispose it again itself.
    /// </summary>
    [Multipart]
    [Post("")]
    Task<ThemeDetailDto> UploadAsync([AliasAs("package")] StreamPart package);

    /// <summary>
    /// Builds a theme from the Panel's own theme-builder page - manifest fields, CSS content, and any
    /// images/fonts - instead of a pre-made .zip. See
    /// <c>RustArchon.Api.Administration.ThemePackageBuilder</c>'s remarks: this is assembled into the
    /// same package shape <see cref="UploadAsync"/> itself accepts and validated identically, so it
    /// throws the same <c>Refit.ApiException</c> (422, <c>ThemeUploadErrorDto</c>) on failure. Every
    /// <see cref="StreamPart"/> in <paramref name="images"/>/<paramref name="fonts"/> is disposed by
    /// Refit once the request completes, same as <see cref="UploadAsync"/>'s own <c>package</c>.
    /// </summary>
    [Multipart]
    [Post("/build")]
    Task<ThemeDetailDto> BuildAsync(
        [AliasAs("Name")] string name,
        [AliasAs("Version")] string version,
        [AliasAs("Description")] string? description,
        [AliasAs("AuthorName")] string? authorName,
        [AliasAs("AuthorEmail")] string? authorEmail,
        [AliasAs("Website")] string? website,
        [AliasAs("UpdateUrl")] string? updateUrl,
        [AliasAs("Css")] string css,
        [AliasAs("Images")] IEnumerable<StreamPart> images,
        [AliasAs("Fonts")] IEnumerable<StreamPart> fonts);

    /// <summary>
    /// Rebuilds <paramref name="id"/>'s own content in place from the theme-builder form - the Panel's
    /// "Save" action when editing an existing theme, as opposed to <see cref="BuildAsync"/>'s "Save as
    /// New Theme". See <c>RustArchon.Api.Administration.ThemeService.ReplaceAsync</c>: the theme you
    /// started editing is what gets updated, not a new row alongside it.
    /// </summary>
    [Multipart]
    [Post("/{id}/build")]
    Task<ThemeDetailDto> ReplaceAsync(
        Guid id,
        [AliasAs("Name")] string name,
        [AliasAs("Version")] string version,
        [AliasAs("Description")] string? description,
        [AliasAs("AuthorName")] string? authorName,
        [AliasAs("AuthorEmail")] string? authorEmail,
        [AliasAs("Website")] string? website,
        [AliasAs("UpdateUrl")] string? updateUrl,
        [AliasAs("Css")] string css,
        [AliasAs("Images")] IEnumerable<StreamPart> images,
        [AliasAs("Fonts")] IEnumerable<StreamPart> fonts);

    [Post("/{id}/activate")]
    Task<ThemeDetailDto> ActivateAsync(Guid id);

    /// <summary>Checks this theme's own <c>UpdateUrl</c> right now - see
    /// <c>RustArchon.Api.Administration.ThemeService.CheckForUpdateAsync</c>.</summary>
    [Post("/{id}/check-update")]
    Task<ThemeDetailDto> CheckForUpdateAsync(Guid id);

    /// <summary>Downloads and installs the update this theme's <c>UpdateUrl</c> reports - see
    /// <c>RustArchon.Api.Administration.ThemeService.InstallUpdateAsync</c>. Throws
    /// <c>Refit.ApiException</c> (422, same <c>ThemeUploadErrorDto</c> shape as <see cref="UploadAsync"/>)
    /// if the download or the package itself fails validation.</summary>
    [Post("/{id}/install-update")]
    Task<ThemeDetailDto> InstallUpdateAsync(Guid id);

    [Delete("/{id}")]
    Task DeleteAsync(Guid id);
}
