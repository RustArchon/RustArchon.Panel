// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RustArchon.Diagnostics;

namespace RustArchon.Panel.Services;

/// <summary>
/// The Panel's public door for the read-only diagnostic logs: <c>GET /diagnostics/logs</c> and <c>/diagnostics/logs/help</c> (ADR-0001's pattern, ADR-0006).
/// The Api is not reachable from outside, so a caller with the log token comes here; this throttles refusals, hands the token to the Api (which alone decides
/// whether it is the live one), adds the Panel's own log entries, and returns the result.
/// </summary>
/// <remarks>
/// <para>
/// The token travels in a header, never the address, so it is not recorded wherever addresses are. It is never logged here. Refusals are the same bare
/// <c>401</c> whatever was wrong; <c>404</c> means no token has ever been created (the feature is off).
/// </para>
/// <para>
/// If the Api cannot be reached (or answers with a server error) the Panel cannot ask it about the token. For a token the Api accepted within the last
/// <see cref="VerifiedLogTokenCache.Lifetime"/> it still answers, from the Panel's own logs, and names every other source as unavailable with the reason; for
/// anything else it answers <c>503</c> and no log data. While the Api answers, every request is checked live, so revocation is immediate.
/// </para>
/// </remarks>
public sealed class LogReadProxy(
    IHttpClientFactory clients, LogRing panelRing, LogReadThrottle throttle, VerifiedLogTokenCache verified, TimeProvider clock, ILogger<LogReadProxy> logger)
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client that carries the internal-service key.</summary>
    public const string ClientName = "InternalLogs";

    /// <summary>The header both the caller and the Api use for the log token. Must match <c>InternalLogsController.TokenHeader</c>.</summary>
    public const string TokenHeader = "X-RustArchon-Log-Token";

    /// <summary>The longest token that is even forwarded. A real one is 52 characters.</summary>
    public const int MaxTokenLength = 128;

    /// <summary>The largest Api answer that will be held in memory to merge or pass on.</summary>
    public const int MaxResponseCharacters = 32 * 1024 * 1024;

    private const string Ndjson = "application/x-ndjson";

    private sealed record Upstream(HttpStatusCode? Status, string Body, string? ContentType, string? Failure);

    public async Task HandleLogsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var response = context.Response;
        response.Headers.CacheControl = "no-store";
        var address = ClientAddress(context);
        if (Blocked(response, address))
        {
            return;
        }

        var token = PresentedToken(context);
        var upstream = await CallApiAsync("/internal/logs" + context.Request.QueryString, token, cancellationToken);

        if (upstream.Failure is not null)
        {
            await ServeWithoutApiAsync(context, address, token, upstream.Failure, cancellationToken);
            return;
        }

        switch (upstream.Status)
        {
            case HttpStatusCode.NotFound:
                response.StatusCode = 404;
                Finish(address, 404);
                return;
            case HttpStatusCode.Unauthorized:
                Refused(address, token);
                response.StatusCode = 401;
                return;
            case HttpStatusCode.BadRequest:
                response.StatusCode = 400;
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync(upstream.Body, cancellationToken);
                Finish(address, 400);
                return;
            case HttpStatusCode.OK:
                break;
            default:
                response.StatusCode = 502;
                Finish(address, 502, $"the Api answered {(int?)upstream.Status}");
                return;
        }

        verified.Remember(token);

        var parsed = LogQueryParser.Parse(QueryParameters(context), clock.GetUtcNow());
        if (!parsed.Ok)
        {
            response.StatusCode = 400;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(parsed.Error!, cancellationToken);
            Finish(address, 400);
            return;
        }

        if (!parsed.Request!.Sources.Contains("panel"))
        {
            response.StatusCode = 200;
            response.ContentType = upstream.ContentType ?? Ndjson;
            await response.WriteAsync(upstream.Body, cancellationToken);
            Finish(address, 200);
            return;
        }

        var local = await new LogQueryEngine(clock).ExecuteAsync(parsed.Request.Query, [new RingLogSource("panel", panelRing)], cancellationToken);
        using var merged = new MemoryStream();
        try
        {
            await LogNdjsonMerger.MergeAsync(upstream.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries), local, merged, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            response.StatusCode = 502;
            Finish(address, 502, ex.Message);
            return;
        }

        response.StatusCode = 200;
        response.ContentType = Ndjson;
        merged.Position = 0;
        await merged.CopyToAsync(response.Body, cancellationToken);
        Finish(address, 200);
    }

    /// <summary>
    /// The diagnostic views door (ADR-0007): <c>GET /diagnostics/views</c> (the catalog) and <c>GET /diagnostics/views/{name}</c>. The same token, throttle and
    /// rejection rules as the logs; the Api's answer is passed through with its status and body, because it is already in its final shape. There is no fallback
    /// when the Api cannot be reached: a view is the Api's own data, so without the Api there is nothing to serve.
    /// </summary>
    public async Task HandleViewsAsync(HttpContext context, string? name, CancellationToken cancellationToken)
    {
        var response = context.Response;
        response.Headers.CacheControl = "no-store";
        var address = ClientAddress(context);
        if (Blocked(response, address))
        {
            return;
        }

        var token = PresentedToken(context);
        var path = "/internal/diagnostics/views" + (name is null ? string.Empty : "/" + Uri.EscapeDataString(name)) + context.Request.QueryString;
        var upstream = await CallApiAsync(path, token, cancellationToken, jsonServerErrorsAreAnswers: true);
        if (upstream.Failure is not null)
        {
            response.StatusCode = 503;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync("The Api cannot be reached, so nothing can be read.", cancellationToken);
            Finish(address, 503, upstream.Failure);
            return;
        }

        switch (upstream.Status)
        {
            case HttpStatusCode.Unauthorized:
                Refused(address, token);
                response.StatusCode = 401;
                return;
            case HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.InternalServerError:
                if (upstream.Status == HttpStatusCode.OK)
                {
                    verified.Remember(token);
                }

                response.StatusCode = (int)upstream.Status!.Value;
                if (upstream.Body.Length > 0)
                {
                    response.ContentType = upstream.ContentType ?? "application/json";
                    await response.WriteAsync(upstream.Body, cancellationToken);
                }

                Finish(address, response.StatusCode, response.StatusCode is 200 or 404 ? null : "the Api reported an error");
                return;
            default:
                response.StatusCode = 502;
                Finish(address, 502, $"the Api answered {(int?)upstream.Status}");
                return;
        }
    }

    public async Task HandleHelpAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var response = context.Response;
        response.Headers.CacheControl = "no-store";
        var address = ClientAddress(context);
        if (Blocked(response, address))
        {
            return;
        }

        var token = PresentedToken(context);
        var upstream = await CallApiAsync("/internal/logs/help", token, cancellationToken);
        if (upstream.Failure is not null)
        {
            response.StatusCode = 503;
            Finish(address, 503, upstream.Failure);
            return;
        }

        switch (upstream.Status)
        {
            case HttpStatusCode.OK:
                verified.Remember(token);
                response.StatusCode = 200;
                response.ContentType = upstream.ContentType ?? "text/plain; charset=utf-8";
                await response.WriteAsync(upstream.Body, cancellationToken);
                Finish(address, 200);
                return;
            case HttpStatusCode.NotFound:
                response.StatusCode = 404;
                Finish(address, 404);
                return;
            case HttpStatusCode.Unauthorized:
                Refused(address, token);
                response.StatusCode = 401;
                return;
            default:
                response.StatusCode = 502;
                Finish(address, 502, $"the Api answered {(int?)upstream.Status}");
                return;
        }
    }

    private async Task ServeWithoutApiAsync(HttpContext context, string address, string token, string failure, CancellationToken cancellationToken)
    {
        var response = context.Response;
        if (!verified.Contains(token))
        {
            response.StatusCode = 503;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync("The Api cannot be reached, so the token cannot be checked.", cancellationToken);
            Finish(address, 503, failure);
            return;
        }

        var parsed = LogQueryParser.Parse(QueryParameters(context), clock.GetUtcNow());
        if (!parsed.Ok)
        {
            response.StatusCode = 400;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(parsed.Error!, cancellationToken);
            Finish(address, 400, failure);
            return;
        }

        var sources = new List<ILogSource>();
        foreach (var name in parsed.Request!.Sources)
        {
            sources.Add(name == "panel"
                ? new RingLogSource("panel", panelRing)
                : new UnavailableSource(name, $"The Api could not be reached ({failure}), so this source cannot be read. The Panel's own entries are still returned."));
        }

        var result = await new LogQueryEngine(clock).ExecuteAsync(parsed.Request.Query, sources, cancellationToken);
        response.StatusCode = 200;
        response.ContentType = Ndjson;
        await LogNdjsonWriter.WriteAsync(result, response.Body, cancellationToken);
        logger.LogWarning(
            "Diagnostic log request from {Address} answered 200 from the Panel's own logs only: the Api could not be reached ({Failure}).", address, failure);
    }

    private async Task<Upstream> CallApiAsync(string pathAndQuery, string token, CancellationToken cancellationToken, bool jsonServerErrorsAreAnswers = false)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
            request.Headers.TryAddWithoutValidation(TokenHeader, token);
            using var response = await clients.CreateClient(ClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // A 500 the Api itself wrote (a JSON error body) is an answer; a 5xx from anything in front of it, or without a body to read, means it cannot be reached.
            var apiAnswered = jsonServerErrorsAreAnswers && response.StatusCode == HttpStatusCode.InternalServerError
                && response.Content.Headers.ContentType?.MediaType == "application/json";
            if ((int)response.StatusCode >= 500 && !apiAnswered)
            {
                return new Upstream(null, string.Empty, null, $"the Api answered {(int)response.StatusCode}");
            }

            if (response.Content.Headers.ContentLength > MaxResponseCharacters * 2L)
            {
                return new Upstream(HttpStatusCode.BadGateway, string.Empty, null, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Length > MaxResponseCharacters
                ? new Upstream(HttpStatusCode.BadGateway, string.Empty, null, null)
                : new Upstream(response.StatusCode, body, response.Content.Headers.ContentType?.ToString(), null);
        }
        catch (HttpRequestException ex)
        {
            return new Upstream(null, string.Empty, null, "connection failed: " + ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Upstream(null, string.Empty, null, "timed out");
        }
    }

    private bool Blocked(HttpResponse response, string address)
    {
        if (!throttle.IsBlocked(address))
        {
            return false;
        }

        response.StatusCode = 429;
        response.Headers.RetryAfter = "60";
        logger.LogWarning("Diagnostic log request from {Address} answered 429: too many refused requests in the last minute.", address);
        return true;
    }

    private void Refused(string address, string token)
    {
        throttle.RecordFailure(address);
        verified.Forget(token);
        logger.LogWarning("Diagnostic log request from {Address} answered 401: the log token was missing or not the live one.", address);
    }

    // Logs the outcome. The status code is set by the caller BEFORE any body is written: once a byte of the body has gone out a real response refuses a
    // new status (this method used to set it, which worked in tests that used a lenient response and failed on the first real request).
    private void Finish(string address, int status, string? detail = null)
    {
        if (status is 200 or 404)
        {
            logger.LogInformation("Diagnostic log request from {Address} answered {Status}.", address, status);
        }
        else
        {
            logger.LogWarning("Diagnostic log request from {Address} answered {Status}. {Detail}", address, status, detail);
        }
    }

    private static string PresentedToken(HttpContext context)
    {
        var token = context.Request.Headers[TokenHeader].ToString();
        return token.Length > MaxTokenLength ? string.Empty : token;
    }

    /// <summary>
    /// The caller's address: Cloudflare's <c>CF-Connecting-IP</c> when present (the Panel sits behind it), otherwise the connection's. Best effort for noise
    /// control only; it is not what keeps the token safe.
    /// </summary>
    internal static string ClientAddress(HttpContext context)
    {
        var cloudflare = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (IPAddress.TryParse(cloudflare, out var parsed))
        {
            return parsed.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> QueryParameters(HttpContext context) =>
        context.Request.Query.ToDictionary(
            q => q.Key, q => (IReadOnlyList<string>)q.Value.Select(v => v ?? string.Empty).ToList(), StringComparer.OrdinalIgnoreCase);

    private sealed class UnavailableSource(string name, string reason) : ILogSource
    {
        public string Name => name;

        public ValueTask<LogSourceResult> ReadAsync(SourceCursor? after, int max, Func<LogEntry, bool> filter, CancellationToken cancellationToken) =>
            ValueTask.FromResult(LogSourceResult.Unavailable(name, reason));
    }
}
