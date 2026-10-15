// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the platform-admin <c>DiagnosticLogAdminController</c>: the one read-only credential for the diagnostic log door (ADR-0006).
/// Only an account holding <c>Platform.ManageSettings</c> can use it - every call is a <c>403</c> otherwise. The secret is in the response to
/// <see cref="CreateAsync"/> and nowhere else, ever.
/// </summary>
public interface IDiagnosticLogAdminApiClient
{
    [Get("")]
    Task<DiagnosticLogTokenStatusDto> GetAsync();

    /// <summary>Creates a token; if one exists it stops working at once.</summary>
    [Post("")]
    Task<DiagnosticLogTokenCreatedDto> CreateAsync();

    [Delete("")]
    Task RevokeAsync();
}
