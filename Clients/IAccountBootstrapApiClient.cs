// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Refit;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Client for the account-bootstrap endpoint (<c>AccountBootstrapController</c> in RustArchon.Api)
/// that provisions a new user's own tenant and Owner role.
/// </summary>
/// <remarks>
/// Passes the identity assertion token explicitly per call, the same way
/// <c>ITokenExchangeApiClient</c> does - see <see cref="Services.NewTenantBootstrapper"/>.
/// </remarks>
public interface IAccountBootstrapApiClient
{
    /// <summary>
    /// Ensures the calling user has a tenant of their own, creating one (with an Owner role) if
    /// they don't already have one.
    /// </summary>
    [Post("/api/account-bootstrap/ensure-tenant")]
    Task EnsureTenantAsync([Header("Authorization")] string bearerAssertionToken, [Query] string? tenantName = null);
}
