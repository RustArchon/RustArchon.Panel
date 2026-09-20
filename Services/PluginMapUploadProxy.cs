// Copyright ©2026 Scott Blomfield

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using RustArchon.Messaging.Contracts;

namespace RustArchon.Panel.Services;

/// <summary>
/// The Panel's public door for a game server's map picture: <c>POST /ingest/plugin-map</c>, with the one-time token in the
/// <c>X-RustArchon-Upload-Token</c> header (not the address, so it is not recorded wherever addresses are). The token is the only
/// credential and names the server and map it was minted for, so the address carries nothing. A game server
/// cannot reach RustArchon.Api (never public), so its plugin posts the picture here and this streams it on to the Api's
/// internal endpoint, which decides whether the single-use token is good and whether the body is a picture. Nothing is decided
/// here beyond the cheapest checks.
/// </summary>
/// <remarks>
/// <para>
/// The body is streamed, never buffered in the Panel: a picture is tens of megabytes. Every refusal about the token - wrong,
/// used, expired, Api down, anything - is the same bare 404, so the response tells a guesser nothing. Only a problem with the
/// picture itself (not a PNG, wrong size) is a 400, and that is only ever seen by whoever already holds a good token.
/// </para>
/// </remarks>
public static class PluginMapUploadProxy
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client that carries the internal-service key.</summary>
    public const string ClientName = "InternalPluginMapUpload";

    /// <summary>The longest token that is even forwarded. A real one is 43 characters.</summary>
    public const int MaxTokenLength = 128;

    /// <summary>The largest picture forwarded; the Api holds the same ceiling and is the one that enforces it.</summary>
    public const long MaxBytes = 120L * 1024 * 1024;

    public static async Task<IResult> HandleAsync(
        HttpRequest request, HttpClient client, CancellationToken cancellationToken)
    {
        string? token = request.Headers[RustArchonPlugin.MapUploadTokenHeader];
        request.HttpContext.Response.Headers.CacheControl = "no-store";

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return Results.NotFound();
        }

        // A picture of a declared, sane size: without a length the body cannot be streamed with one, and a huge one is
        // refused before a byte is read. Same bare 404 as a bad token, so this reveals nothing either.
        if (request.ContentLength is null or <= 0 or > MaxBytes)
        {
            return Results.NotFound();
        }

        // Kestrel's own default ceiling is smaller than a map; lift it for this request only, up to the same limit.
        var sizeLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false })
        {
            sizeLimit.MaxRequestBodySize = MaxBytes;
        }

        try
        {
            using var content = new StreamContent(request.Body);
            content.Headers.Add(RustArchonPlugin.MapUploadTokenHeader, token);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Headers.ContentLength = request.ContentLength;

            using var upstream = await client.PostAsync(
                "/internal/plugin/map", content, cancellationToken);

            return upstream.StatusCode switch
            {
                HttpStatusCode.NoContent or HttpStatusCode.OK => Results.NoContent(),
                HttpStatusCode.BadRequest => Results.BadRequest(),
                _ => Results.NotFound()
            };
        }
        catch (HttpRequestException)
        {
            return Results.NotFound();
        }
    }
}
