// Copyright ©2026 Scott Blomfield

using System.Threading;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit client for the one internal (shared-secret, non-JWT) endpoint the Stripe webhook route calls -
/// same channel and reasoning as <see cref="IInternalCommunicationApiClient"/>. Only ever called from
/// <see cref="Services.StripeWebhookHandler"/>, after it has independently verified a payload's Stripe
/// signature - RustArchon.Api trusts this call unconditionally, the same way it trusts every other
/// caller authenticated on this channel.
/// </summary>
public interface IInternalStripeApiClient
{
    /// <summary>
    /// This deployment's Stripe webhook signing secret, decrypted - fetched live on every inbound
    /// webhook delivery rather than cached, the same as every other Secret-typed platform setting (see
    /// <c>StripeCredentialProvider</c>'s own remarks in RustArchon.Api).
    /// </summary>
    [Get("/internal/stripe/webhook-secret")]
    Task<string> GetWebhookSecretAsync(CancellationToken cancellationToken = default);

    [Post("/internal/stripe/payments")]
    Task RecordPaymentAsync([Body] RecordStripePaymentRequestDto request, CancellationToken cancellationToken = default);

    [Post("/internal/stripe/payments/failed")]
    Task RecordFailedPaymentAsync(
        [Body] RecordFailedStripePaymentRequestDto request, CancellationToken cancellationToken = default);

    [Post("/internal/stripe/disputes")]
    Task RecordDisputeAsync(
        [Body] RecordStripeDisputeRequestDto request, CancellationToken cancellationToken = default);
}
