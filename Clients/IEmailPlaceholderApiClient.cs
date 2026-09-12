// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for <c>EmailPlaceholdersController</c> - the reusable <c>{{Token}}</c> placeholders
/// email templates draw from. Gated the same way <see cref="IEmailTemplateApiClient"/> is
/// (<c>Platform.ManageSettings</c>); no Create/Delete, and no way to change a placeholder's Name - see
/// that controller's remarks.
/// </summary>
public interface IEmailPlaceholderApiClient
{
    [Get("")]
    Task<List<EmailPlaceholderDto>> GetAllAsync();

    [Get("/{name}")]
    Task<EmailPlaceholderDto> GetAsync(string name);

    [Put("/{name}")]
    Task<EmailPlaceholderDto> UpdateAsync(string name, [Body] UpdateEmailPlaceholderDto request);
}
