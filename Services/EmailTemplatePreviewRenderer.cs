// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Net;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// Substitutes each placeholder's real <see cref="EmailPlaceholderDto.Sample"/> value into a
/// template's <c>{{Token}}</c> placeholders, for Admin/EmailTemplates.razor's live preview.
/// </summary>
/// <remarks>
/// Preview only - never touches what's actually saved, and has nothing to do with
/// <c>RustArchon.Api.Administration.EmailTemplateRenderer</c>, which substitutes real values at send
/// time. Sample values are HTML-encoded before substitution into the body (never into the subject, a
/// plain-text header) for the same reason that renderer encodes real ones: an admin could set a
/// sample containing HTML without meaning to embed markup.
/// </remarks>
public static class EmailTemplatePreviewRenderer
{
    /// <summary>
    /// <paramref name="siteName"/>/<paramref name="siteUrl"/> substitute for <c>{{SiteName}}</c>/
    /// <c>{{SiteUrl}}</c> - and feed the branded shell itself - regardless of whether the template
    /// being previewed happens to list them among its own <paramref name="placeholders"/>, mirroring
    /// <c>CommunicationPublisher.QueueTemplatedAsync</c>'s real send path: those two are available in
    /// every email automatically, so the preview should show that too, not just what's explicitly
    /// linked.
    /// </summary>
    public static (string Subject, string HtmlBody) Render(
        string subject, string htmlBody, IEnumerable<EmailPlaceholderDto> placeholders,
        string siteName, string siteUrl)
    {
        // Substituted first, and with the live values rather than a Sample, so a template that also
        // happens to have SiteName/SiteUrl linked among its own placeholders (see this method's own
        // remarks) still shows the real current name/URL - by the time the loop below reaches that
        // placeholder, if any, the token is already gone and its own Replace is a harmless no-op.
        subject = subject.Replace("{{SiteName}}", siteName, StringComparison.Ordinal);
        htmlBody = htmlBody.Replace("{{SiteName}}", WebUtility.HtmlEncode(siteName), StringComparison.Ordinal);
        subject = subject.Replace("{{SiteUrl}}", siteUrl, StringComparison.Ordinal);
        htmlBody = htmlBody.Replace("{{SiteUrl}}", WebUtility.HtmlEncode(siteUrl), StringComparison.Ordinal);

        foreach (var placeholder in placeholders)
        {
            var token = "{{" + placeholder.Name + "}}";
            subject = subject.Replace(token, placeholder.Sample, StringComparison.Ordinal);
            htmlBody = htmlBody.Replace(token, WebUtility.HtmlEncode(placeholder.Sample), StringComparison.Ordinal);
        }

        return (subject, EmailLayoutPreview.Wrap(htmlBody, siteName, siteUrl));
    }
}
