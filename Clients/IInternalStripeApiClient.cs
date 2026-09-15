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
    [Post("/internal/stripe/payments")]
    Task RecordPaymentAsync([Body] RecordStripePaymentRequestDto request, CancellationToken cancellationToken = default);
}
