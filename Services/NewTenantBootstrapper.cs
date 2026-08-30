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
/// Deliberately best-effort: a failure here does not block registration, since the alternative (a
/// user who can't create an account at all because a provisioning call is unreachable) is worse than
/// "the account exists but has no tenant yet" - a state a later call can still recover, since
/// provisioning is idempotent.
/// </remarks>
public class NewTenantBootstrapper(
    IJwtTokenService jwtTokenService,
    IAccountBootstrapApiClient accountBootstrapClient,
    ILogger<NewTenantBootstrapper> logger)
{
    private static readonly TimeSpan AssertionTokenLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Ensures the newly created user has their own tenant and Owner role. Best-effort - logs and
    /// swallows failures rather than propagating them, since this must never block registration.
    /// </summary>
    /// <param name="userId">The newly created user's ID.</param>
    /// <param name="username">The newly created user's username (for the identity assertion).</param>
    public async Task ProvisionAsync(Guid userId, string username)
    {
        try
        {
            var assertionToken = jwtTokenService.GenerateToken(userId, username, expiration: AssertionTokenLifetime);
            await accountBootstrapClient.EnsureTenantAsync($"Bearer {assertionToken}", tenantName: $"{username}'s Organization");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to provision a tenant for new user {UserId}", userId);
        }
    }
}
