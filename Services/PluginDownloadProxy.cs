// Copyright ©2026 Scott Blomfield

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RustArchon.Messaging.Contracts;

namespace RustArchon.Panel.Services;

/// <summary>
/// The Panel's public door for the RustArchon Updater: <c>GET /ingest/plugin/{serverId}/{token}</c>. A game server
/// cannot reach RustArchon.Api (never public), so its Updater fetches the new plugin here and this hands the request
/// to Api's internal endpoint, which decides whether the single-use token is good. Nothing is decided here.
/// </summary>
/// <remarks>
/// Every refusal - wrong token, used token, expired, Api down, anything - is the same bare 404, so the response tells
/// a guesser nothing and never distinguishes "no such token" from "token already spent". The body is the script's
/// exact bytes: it is signed, and anything that re-encoded it would break the signature.
/// </remarks>
public static class PluginDownloadProxy
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client that carries the internal-service key.</summary>
    public const string ClientName = "InternalPluginDownload";

    /// <summary>The longest token that is even forwarded. A real one is 43 characters.</summary>
    public const int MaxTokenLength = 128;

    /// <summary>
    /// The door an Updater from 0.3.0 uses: the token comes in the <c>X-RustArchon-Update-Token</c> header (so no access log or proxy
    /// keeps it) and is passed on to Api in the same header; Api works out the server from it. Like the address form, every refusal is a
    /// bare 404 and nothing is ever cached.
    /// </summary>
    public static async Task<IResult> HandleHeaderTokenAsync(
        HttpRequest request, HttpClient client, HttpResponse response, CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";

        string? token = request.Headers[RustArchonPlugin.UpdateTokenHeader];
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return Results.NotFound();
        }

        try
        {
            using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, "/internal/plugin/download");
            upstreamRequest.Headers.TryAddWithoutValidation(RustArchonPlugin.UpdateTokenHeader, token);
            using var upstream = await client.SendAsync(upstreamRequest, cancellationToken);

            if (!upstream.IsSuccessStatusCode)
            {
                return Results.NotFound();
            }

            var bytes = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);
            if (upstream.Headers.TryGetValues("X-RustArchon-Plugin-Version", out var version))
            {
                response.Headers["X-RustArchon-Plugin-Version"] = string.Join(",", version);
            }

            return Results.File(bytes, "text/plain; charset=utf-8", "RustArchon.cs");
        }
        catch (HttpRequestException)
        {
            return Results.NotFound();
        }
    }

    public static async Task<IResult> HandleAsync(
        Guid serverId, string token, HttpClient client, HttpResponse response, CancellationToken cancellationToken)
    {
        // Never cache, wherever the answer ends up - success or not.
        response.Headers.CacheControl = "no-store";

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return Results.NotFound();
        }

        try
        {
            using var upstream = await client.GetAsync(
                $"/internal/plugin/download/{serverId:D}/{Uri.EscapeDataString(token)}", cancellationToken);

            if (!upstream.IsSuccessStatusCode)
            {
                return Results.NotFound();
            }

            var bytes = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);
            if (upstream.Headers.TryGetValues("X-RustArchon-Plugin-Version", out var version))
            {
                response.Headers["X-RustArchon-Plugin-Version"] = string.Join(",", version);
            }

            return Results.File(bytes, "text/plain; charset=utf-8", "RustArchon.cs");
        }
        catch (HttpRequestException)
        {
            return Results.NotFound();
        }
    }
}
