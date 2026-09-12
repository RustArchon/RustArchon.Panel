// Copyright ©2026 Scott Blomfield

using System.Net;

namespace RustArchon.Panel.Services;

/// <summary>
/// Mirrors <c>RustArchon.Api.Administration.EmailLayout</c> exactly, for
/// <see cref="EmailTemplatePreviewRenderer"/>'s preview - duplicated rather than referenced, the same
/// way small shapes are duplicated between the Api/Worker/Panel elsewhere in this app (see
/// <c>RustArchon.Worker</c>'s own <c>EmailProviders</c> remarks), since the Panel must never take a
/// project reference on the Api. If the real shell changes, this one needs to change with it.
/// </summary>
public static class EmailLayoutPreview
{
    private const string BackgroundColor = "#f2efe6";
    private const string CardColor = "#fbf9f3";
    private const string BorderColor = "#d3cdb8";
    private const string HeaderColor = "#211f18";
    private const string TextColor = "#211f18";
    private const string MutedTextColor = "#55523f";
    private const string AccentColor = "#ff6a1a";

    public static string Wrap(string contentHtml, string siteName, string siteUrl)
    {
        var encodedName = WebUtility.HtmlEncode(siteName);
        var encodedUrl = WebUtility.HtmlEncode(siteUrl);

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{BackgroundColor};padding:32px 16px;">
            <tr><td align="center">
            <table role="presentation" width="100%" style="max-width:560px;background-color:{CardColor};border:1px solid {BorderColor};border-radius:8px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;" cellpadding="0" cellspacing="0">
            <tr><td style="background-color:{HeaderColor};padding:20px 28px;">
            <a href="{encodedUrl}" style="font-size:20px;font-weight:bold;letter-spacing:2px;color:{AccentColor};text-transform:uppercase;text-decoration:none;">{encodedName}</a>
            </td></tr>
            <tr><td style="padding:28px;color:{TextColor};font-size:15px;line-height:1.6;">
            {contentHtml}
            </td></tr>
            <tr><td style="padding:16px 28px;border-top:1px solid {BorderColor};color:{MutedTextColor};font-size:12px;">
            This is an automated message from {encodedName}.
            </td></tr>
            </table>
            </td></tr>
            </table>
            """;
    }
}
