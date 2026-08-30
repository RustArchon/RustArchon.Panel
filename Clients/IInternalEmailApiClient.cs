// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for RustArchon.Api's internal (shared-secret, non-JWT) email endpoint. Registered in
/// <c>Program.cs</c> with the <c>X-Internal-Api-Key</c> header attached directly to the underlying
/// <see cref="System.Net.Http.HttpClient"/> - unlike every other API client in this project, it
/// carries no JWT handlers, since this call authenticates as "the RustArchon web app itself," not as
/// any particular signed-in user (there frequently isn't one - see <c>QueuedEmailSender</c>'s remarks).
/// </summary>
public interface IInternalEmailApiClient
{
    [Post("/internal/email")]
    Task SendAsync([Body] SendEmailRequestDto request);
}
