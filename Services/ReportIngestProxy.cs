// Copyright ©2026 Scott Blomfield

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace RustArchon.Panel.Services;

/// <summary>
/// The Panel's public door for in-game (F7) reports: <c>POST /ingest/reports/{serverId}/{token}</c>, the address a game server's
/// <c>server.reportsServerEndpoint</c> points at (ADR-0001). A game server cannot reach RustArchon.Api (never public), so it posts
/// here and this streams the form on to the Api's internal endpoint, which decides whether the token is that server's and whether the
/// body is a report. Nothing is decided here beyond the cheapest checks.
/// </summary>
/// <remarks>
/// <para>
/// The token is the only credential, and it is in the address because Rust's endpoint can send nothing else (no headers, no
/// signature). It is carried on to the Api in a header rather than the address, so it is not recorded wherever the Api's addresses
/// are, and this route is never logged: the Panel's request logging is held at Warning (<c>Microsoft.AspNetCore</c> in
/// appsettings.json), which is what keeps the address out of the log. A reverse proxy in front may still record it, which is why the
/// token is per server, worth only one server's report inbox, and rotatable.
/// </para>
/// <para>
/// The body is streamed, never buffered here. Every refusal - bad token, unknown server, throttled, Api down, anything - is the same
/// bare 404, so the response tells a guesser nothing.
/// </para>
/// </remarks>
public static class ReportIngestProxy
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client that carries the internal-service key.</summary>
    public const string ClientName = "InternalReportIngest";

    /// <summary>The header the Api reads the token from. Must match <c>InternalReportIngestController.TokenHeader</c>.</summary>
    public const string TokenHeader = "X-RustArchon-Report-Token";

    /// <summary>The name of the per-address rate-limit policy registered in <c>Program.cs</c>.</summary>
    public const string RateLimitPolicy = "report-ingest";

    /// <summary>The longest token that is even forwarded. A real one is 43 characters.</summary>
    public const int MaxTokenLength = 128;

    /// <summary>
    /// The most a report post may weigh. A guard against abuse, not a screenshot cap: it sits far above the largest picture the game
    /// sends. The Api holds the same ceiling and is the one that authenticates before reading.
    /// </summary>
    public const long MaxBytes = 64L * 1024 * 1024;

    public static async Task<IResult> HandleAsync(
        Guid serverId, string token, HttpRequest request, HttpClient client, CancellationToken cancellationToken)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return Results.NotFound();
        }

        // The game posts a form; anything else is not it, and a body of any other kind is not forwarded.
        if (!request.HasFormContentType || request.ContentLength is <= 0 or > MaxBytes)
        {
            return Results.NotFound();
        }

        // Kestrel's own default ceiling is smaller than a big screenshot; lift it for this request only, up to the same limit. This
        // is also what bounds a body that arrives without a declared length.
        var sizeLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false })
        {
            sizeLimit.MaxRequestBodySize = MaxBytes;
        }

        try
        {
            using var content = new StreamContent(request.Body);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType!);
            if (request.ContentLength is { } length)
            {
                content.Headers.ContentLength = length;
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, $"/internal/reports/{serverId}") { Content = content };
            message.Headers.Add(TokenHeader, token);

            using var upstream = await client.SendAsync(message, cancellationToken);

            return upstream.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (Exception ex) when (ex is HttpRequestException or BadHttpRequestException or FormatException)
        {
            return Results.NotFound();
        }
    }
}
