// Copyright ©2026 Scott Blomfield

using System;
using System.Threading.Tasks;
using JumpStart.Services.Authentication;
using Microsoft.Extensions.Logging;
using RustArchon.Panel.Clients;

namespace RustArchon.Panel.Services;

/// <summary>
/// Provisions a brand-new user with their own tenant and an Owner role immediately after their
/// account is created, so they can start adding Rust servers right away instead of 403-ing on their
/// first request. Mirrors JumpStart's own <c>DemoNewUserBootstrapper</c> pattern.
/// </summary>
/// <remarks>
/// Called directly from the account-creation code paths (<c>Register.razor</c>,
/// <c>ExternalLogin.razor</c>) - both already know the new user's real ID and username at the moment
/// their account is created, so this mints a short-lived identity assertion and calls the bootstrap
/// endpoint right there, needing none of <c>JwtExchangeHandler</c>'s circuit-scoped machinery.
/// <para>
/// It reports whether it worked rather than deciding what a failure means. It used to swallow the
/// exception on the reasoning that "the account exists but has no tenant yet" is recoverable, since
/// provisioning is idempotent - but nothing ever retried it. <c>EnsureTenant</c> is called from
/// account creation and nowhere else, so a swallowed failure left an account that could never do
/// anything, having already spent a single-use invitation code to get there. The caller now rolls the
/// whole registration back instead.
/// </para>
/// </remarks>
public class NewTenantBootstrapper(
    IJwtTokenService jwtTokenService,
    IAccountBootstrapApiClient accountBootstrapClient,
    ILogger<NewTenantBootstrapper> logger)
{
    private static readonly TimeSpan AssertionTokenLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Ensures the newly created user has their own tenant and Owner role.
    /// </summary>
    /// <param name="userId">The newly created user's ID.</param>
    /// <param name="username">The newly created user's username (for the identity assertion).</param>
    /// <returns>
    /// <c>false</c> if provisioning did not complete. The exception is logged and not rethrown - the
    /// caller's job is to undo the registration, not to render a stack trace - but it is reported,
    /// because an account with no organization is not a registration that succeeded.
    /// </returns>
    public async Task<bool> ProvisionAsync(Guid userId, string username)
    {
        try
        {
            var assertionToken = jwtTokenService.GenerateToken(userId, username, expiration: AssertionTokenLifetime);
            await accountBootstrapClient.EnsureTenantAsync($"Bearer {assertionToken}", tenantName: $"{username}'s Organization");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision a tenant for new user {UserId}", userId);
            return false;
        }
    }
}
