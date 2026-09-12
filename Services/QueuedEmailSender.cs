// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using RustArchon.Panel.Clients;
using RustArchon.Panel.Data;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// Replaces <c>IdentityNoOpEmailSender</c> (removed) as the registered <see cref="IEmailSender{TUser}"/>
/// - see <c>Program.cs</c>. Every call queues an email through one of RustArchon.Api's admin-editable
/// <c>EmailTemplate</c>s (see <see cref="IdentityEmailTemplates"/>) rather than building HTML here - a
/// <c>RustArchon.Worker</c> instance picks up the resulting message and does the actual send.
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
        SendAsync(
            user, email, IdentityEmailTemplates.EmailConfirmation,
            new Dictionary<string, string> { [IdentityEmailTemplates.ConfirmationLink] = confirmationLink });

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(
            user, email, IdentityEmailTemplates.PasswordResetLink,
            new Dictionary<string, string> { [IdentityEmailTemplates.ResetLink] = resetLink });

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(
            user, email, IdentityEmailTemplates.PasswordResetCode,
            new Dictionary<string, string> { [IdentityEmailTemplates.ResetCode] = resetCode });

    // UserId threaded through on every call - account-level, so never a TenantId (that's only for an
    // organization-level communication - see Communication.TenantId's remarks). user.Id is always
    // real here: every IEmailSender<TUser> method hands us the actual account, even when nobody is
    // signed in to this circuit (see this class's own remarks on the several auth states it's called
    // from) - the account this email is *about* is never in question, only who's asking for it.
    private Task SendAsync(
        ApplicationUser user, string email, string templateCode, Dictionary<string, string> tokens) =>
        client.SendTemplatedAsync(new SendTemplatedEmailRequestDto
        {
            To = email,
            TemplateCode = templateCode,
            Tokens = tokens,
            UserId = user.Id,
            Culture = user.PreferredCulture
        });
}
