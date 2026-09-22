// Copyright ©2026 Scott Blomfield

using System;
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

    /// <summary>Queues an email built from an admin-editable template - see
    /// <c>RustArchon.Api.Infrastructure.EmailTemplateRegistry.Codes</c> for valid codes.</summary>
    [Post("/internal/email/templated")]
    Task SendTemplatedAsync([Body] SendTemplatedEmailRequestDto request);
}

/// <summary>
/// Refit client for discarding the empty organization a half-finished registration left behind.
/// </summary>
/// <remarks>
/// Same shared-secret channel and the same reasoning as <see cref="IInternalEmailApiClient"/>: there
/// is nobody to authenticate as at that moment. The account that founded the organization is being
/// deleted in the same breath, and it holds its permissions inside the very tenant being discarded.
/// </remarks>
public interface IInternalRegistrationApiClient
{
    // (see below for the profile client)
    /// <summary>
    /// Discards the organization <paramref name="userId"/> just founded, if it is still empty.
    /// </summary>
    /// <returns>Whether one was discarded - <c>false</c> is ordinary, not a failure.</returns>
    [Post("/internal/registrations/{userId}/discard-organization")]
    Task<bool> DiscardOrganizationAsync(Guid userId);
}

/// <summary>
/// Refit client for keeping a person's own settings (see <c>RustArchon.Api.Data.UserProfile</c>) in step with what the Panel learns: their language at
/// sign-up and whenever they switch it. Same shared-secret channel as the other internal clients - at sign-up there is no signed-in person yet.
/// </summary>
public interface IInternalUserProfileApiClient
{
    /// <summary>Sets the person's settings (replacing them). Creates the profile if there is none.</summary>
    [Put("/internal/users/{userId}/profile")]
    Task<UserProfileDto> SetAsync(Guid userId, [Body] UpdateUserProfileDto settings);

    /// <summary>Hands over languages the identity database holds, for people with no profile yet. Never overwrites. Returns how many profiles it created.</summary>
    [Post("/internal/users/profiles/backfill")]
    Task<int> BackfillAsync([Body] BackfillUserProfilesDto batch);
}
