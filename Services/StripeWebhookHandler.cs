// Copyright ©2026 Scott Blomfield

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RustArchon.Panel.Clients;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Services;

/// <summary>
/// Verifies and processes an inbound Stripe webhook payload - the whole reason this lives in
/// RustArchon.Panel rather than RustArchon.Api.
/// </summary>
/// <remarks>
/// <para>
/// <strong>RustArchon.Api is never reachable from outside the Docker network</strong> (see its own
/// <c>InternalController</c> remarks) - the same reason the email tracking pixel and theme assets are
/// served from here instead of proxied there directly. Stripe's servers need to reach this endpoint
/// from the public internet, so this Panel is the one public door, and it makes one internal,
/// shared-secret-authenticated call (<see cref="IInternalStripeApiClient"/>) once it has verified the
/// payload is genuine - exactly the same shape as <c>IInternalCommunicationApiClient</c>'s.
/// </para>
/// <para>
/// <strong>No Stripe.net dependency here on purpose.</strong> Verifying a webhook signature is just
/// HMAC-SHA256 over a documented string, and the handful of JSON fields this needs are read directly
/// with <see cref="JsonDocument"/> - pulling in the full Stripe SDK just for that would make this Panel
/// depend on Stripe's typed models for a payload it never calls Stripe's API with. The API-calling side
/// (<c>StripeCheckoutService</c>, in RustArchon.Api) is the only place that dependency actually earns
/// its keep.
/// </para>
/// </remarks>
public class StripeWebhookHandler(
    IInternalStripeApiClient internalStripeApiClient, IConfiguration configuration,
    ILogger<StripeWebhookHandler> logger)
{
    /// <summary>
    /// How far a signature's own timestamp may drift from now before it's refused - the same 300-second
    /// default Stripe's own libraries use, wide enough for ordinary clock skew and network delay,
    /// narrow enough that a captured, replayed payload has a real expiry.
    /// </summary>
    private static readonly TimeSpan ToleranceWindow = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Verifies <paramref name="body"/> against <paramref name="signatureHeader"/> and, if it's a paid
    /// <c>checkout.session.completed</c> event, records the payment.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the payload was genuinely from Stripe (whether or not it was an event this method
    /// acts on) - the caller should respond 200 either way, since Stripe retries anything but a 2xx.
    /// <c>false</c> only for a signature that doesn't verify, which the caller should answer with 400.
    /// </returns>
    public async Task<bool> ProcessAsync(string? signatureHeader, string body, CancellationToken cancellationToken)
    {
        var webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            logger.LogError("Rejected a Stripe webhook payload - STRIPE_WEBHOOK_SECRET is not configured.");
            return false;
        }

        if (!VerifySignature(body, signatureHeader, webhookSecret))
        {
            logger.LogWarning("Rejected a Stripe webhook payload that failed signature verification.");
            return false;
        }

        try
        {
            await HandleEventAsync(body, cancellationToken);
        }
        catch (Exception ex)
        {
            // The payload is genuinely from Stripe (verified above) but something about acting on it
            // failed - logged, not rethrown, so this still answers 200. A malformed or unexpected event
            // shape from Stripe itself is not something retrying the same payload would ever fix.
            logger.LogError(ex, "Failed to process a verified Stripe webhook payload.");
        }

        return true;
    }

    /// <summary>
    /// The scheme Stripe itself signs with: <c>t=&lt;unix seconds&gt;,v1=&lt;hex HMAC-SHA256 of
    /// "{timestamp}.{payload}"&gt;</c> (see Stripe's own webhook signing docs) - reimplemented by hand
    /// rather than via the Stripe SDK, see this class's own remarks.
    /// </summary>
    private static bool VerifySignature(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        long? timestamp = null;
        string? providedSignature = null;

        foreach (var part in signatureHeader.Split(','))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length != 2)
            {
                continue;
            }

            switch (pieces[0])
            {
                case "t" when long.TryParse(pieces[1], out var t):
                    timestamp = t;
                    break;
                case "v1":
                    providedSignature = pieces[1];
                    break;
            }
        }

        if (timestamp is null || providedSignature is null)
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        if (age.Duration() > ToleranceWindow)
        {
            return false;
        }

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        // Constant-time comparison - an HMAC verification that short-circuits on the first mismatched
        // character leaks, byte by byte, exactly the information a timing attack needs to forge one.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(providedSignature));
    }

    private async Task HandleEventAsync(string body, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeElement) ||
            typeElement.GetString() != "checkout.session.completed")
        {
            return;
        }

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var session))
        {
            logger.LogWarning("checkout.session.completed event carried no session payload - ignored.");
            return;
        }

        // A Checkout Session can complete with nothing actually collected yet (e.g. a bank-transfer
        // payment method still processing) - PaymentStatus, not the event firing at all, is what says
        // money genuinely changed hands. See Stripe's own Session.payment_status docs.
        var paymentStatus = session.TryGetProperty("payment_status", out var statusElement)
            ? statusElement.GetString()
            : null;

        if (paymentStatus != "paid")
        {
            logger.LogInformation(
                "checkout.session.completed has payment_status '{Status}' - not yet paid, nothing recorded.",
                paymentStatus);
            return;
        }

        // Metadata first (set explicitly by StripeCheckoutService), client_reference_id as a fallback -
        // see that class's own remarks on why both are carried.
        string? invoiceIdRaw = null;
        if (session.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("InvoiceId", out var metadataInvoiceId))
        {
            invoiceIdRaw = metadataInvoiceId.GetString();
        }

        invoiceIdRaw ??= session.TryGetProperty("client_reference_id", out var clientReferenceId)
            ? clientReferenceId.GetString()
            : null;

        if (!Guid.TryParse(invoiceIdRaw, out var invoiceId))
        {
            logger.LogWarning("checkout.session.completed carried no usable InvoiceId - ignored.");
            return;
        }

        var amountTotalCents = session.TryGetProperty("amount_total", out var amountElement)
            ? amountElement.GetInt64()
            : 0;
        var amount = amountTotalCents / 100m;

        if (amount <= 0m)
        {
            logger.LogWarning(
                "checkout.session.completed for invoice {InvoiceId} had a non-positive amount_total - ignored.",
                invoiceId);
            return;
        }

        var sessionId = session.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;

        // The PaymentIntent, not the Session, is Stripe's own id for the actual charge - preferred as
        // the idempotency key since a PaymentIntent is the thing that can also show up on a refund/
        // dispute webhook later. Falls back to the Session id only if a PaymentIntent isn't present.
        var providerPaymentId = session.TryGetProperty("payment_intent", out var paymentIntentElement)
            && paymentIntentElement.ValueKind == JsonValueKind.String
            ? paymentIntentElement.GetString()
            : sessionId;

        if (string.IsNullOrEmpty(providerPaymentId))
        {
            logger.LogWarning(
                "checkout.session.completed for invoice {InvoiceId} carried no payment_intent or session id " +
                "to use as an idempotency key - ignored.", invoiceId);
            return;
        }

        await internalStripeApiClient.RecordPaymentAsync(
            new RecordStripePaymentRequestDto
            {
                InvoiceId = invoiceId,
                Amount = amount,
                ProviderPaymentId = providerPaymentId
            },
            cancellationToken);

        logger.LogInformation(
            "Recorded Stripe payment for invoice {InvoiceId}: {Amount} (session {SessionId}).",
            invoiceId, amount, sessionId);
    }
}
