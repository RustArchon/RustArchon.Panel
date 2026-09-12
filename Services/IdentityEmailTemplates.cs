// Copyright ©2026 Scott Blomfield

namespace RustArchon.Panel.Services;

/// <summary>
/// The subset of <c>RustArchon.Api.Infrastructure.EmailTemplateRegistry.Codes</c>/<c>Placeholders</c>
/// that <see cref="QueuedEmailSender"/> needs - duplicated rather than referenced, the same way
/// <c>RustArchon.Worker</c>'s own <c>EmailProviders</c> mirrors the Api's copy: the Panel must never
/// take a project reference on the Api (it would pull EF Core/Npgsql/ASP.NET Core hosting into a
/// process that only ever talks to it over HTTP).
/// </summary>
public static class IdentityEmailTemplates
{
    public const string EmailConfirmation = "EmailConfirmation";
    public const string PasswordResetLink = "PasswordResetLink";
    public const string PasswordResetCode = "PasswordResetCode";

    public const string ConfirmationLink = "ConfirmationLink";
    public const string ResetLink = "ResetLink";
    public const string ResetCode = "ResetCode";
}
