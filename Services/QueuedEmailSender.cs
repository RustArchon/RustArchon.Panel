// Copyright ©2026 Scott Blomfield

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Data;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// Replaces <c>IdentityNoOpEmailSender</c> (removed) as the registered <see cref="IEmailSender{TUser}"/>
/// - see <c>Program.cs</c>. Every call queues an <c>EmailRequested</c> message via
/// <c>RustArchon.Api</c>'s internal endpoint rather than sending anything itself; a
/// <c>RustArchon.Worker</c> instance picks it up and does the actual send (currently still a logged
/// placeholder - see <c>NoOpEmailDeliveryProvider</c> - until a real provider is chosen).
/// </summary>
/// <remarks>
/// <para>
/// Called from several different auth states - an anonymous visitor requesting a password reset, a
/// brand-new user mid-registration, an already-authenticated user changing their email - none of
/// which reliably have a user JWT available. <see cref="IInternalEmailApiClient"/> is registered with
/// no JWT handlers for exactly this reason: it authenticates as "the RustArchon web app," not as the
/// end user the email happens to be about.
/// </para>
/// <para>
/// Callers (<c>Register.razor</c>, <c>ExternalLogin.razor</c>, and Identity's own
/// <c>ForgotPassword</c>/<c>Manage/Email</c> pages) already treat a thrown exception here as
/// non-fatal - see Register.razor's remarks on why the confirmation-email step is wrapped in a
/// try/catch. A failure here now means "couldn't reach RustArchon.Api to queue the message" (the
/// broker being briefly unreachable, say), not "the email failed to send" - that failure mode is
/// handled entirely on the Worker side via MassTransit's retry policy, never surfaced back here.
/// </para>
/// </remarks>
public class QueuedEmailSender(IInternalEmailApiClient client) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");

    private Task SendAsync(string email, string subject, string htmlMessage) =>
        client.SendAsync(new SendEmailRequestDto { To = email, Subject = subject, HtmlBody = htmlMessage });
}
