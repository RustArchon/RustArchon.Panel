// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>EmailTemplatesController</c> - editable Subject/HtmlBody for every kind of
/// email RustArchon sends. Gated the same way <see cref="IPlatformSettingsApiClient"/> is
/// (<c>Platform.ManageSettings</c>); not <see cref="JumpStart.Api.Clients.IApiClient{TDto,TCreateDto,TUpdateDto}"/>-based
/// for the same reason that one isn't - no Create/Delete, and Update is keyed by string Code rather
/// than <c>Guid</c>.
/// </summary>
public interface IEmailTemplateApiClient
{
    [Get("")]
    Task<List<EmailTemplateDto>> GetAllAsync();

    [Get("/{code}")]
    Task<EmailTemplateDto> GetAsync(string code);

    /// <summary>Creates or overwrites one language's Subject/HtmlBody - see
    /// <c>UpdateEmailTemplateTranslationDto.Culture</c>.</summary>
    [Put("/{code}/translations")]
    Task<EmailTemplateDto> UpsertTranslationAsync(string code, [Body] UpdateEmailTemplateTranslationDto request);

    /// <summary>Sets which placeholders this template uses - the whole set, not a delta.</summary>
    [Put("/{code}/placeholders")]
    Task<EmailTemplateDto> UpdatePlaceholdersAsync(string code, [Body] UpdateEmailTemplatePlaceholdersDto request);
}
