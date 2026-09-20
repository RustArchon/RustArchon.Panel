// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the Api's <c>IntegrationVerificationController</c>: asking a third-party provider whether a key an admin typed is one
/// it accepts, before that key is saved onto a server.
/// </summary>
public interface IIntegrationApiClient
{
    /// <summary>
    /// Checks a key with its provider. The key is not stored by this call. A <c>429</c> means the caller is checking too fast and a
    /// <c>403</c> means they may not add servers.
    /// </summary>
    [Post("/verify-key")]
    Task<VerifyIntegrationKeyResultDto> VerifyKeyAsync([Body] VerifyIntegrationKeyRequest request);
}
