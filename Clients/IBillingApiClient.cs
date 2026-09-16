// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for <c>BillingController</c> - platform-admin settlement of invoices, gated
/// server-side by the <c>ManageBilling</c> permission.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IReportApiClient"/>, which only reads. Everything here changes what the
/// books say, which is why it sits behind its own permission: chasing an invoice and writing one off
/// are not the same job.
/// </remarks>
public interface IBillingApiClient
{
    /// <summary>Invoices for the admin screen, newest first.</summary>
    [Get("/invoices")]
    Task<List<InvoiceDto>> GetInvoicesAsync(InvoiceStatus? status = null, Guid? tenantId = null);

    /// <summary>Records money received. Returns the invoice as it stands afterwards.</summary>
    [Post("/payments")]
    Task<InvoiceDto> RecordPaymentAsync([Body] RecordPaymentRequestDto request);

    /// <summary>
    /// Undoes some or all of a payment - a refund or a chargeback. For a Stripe-collected payment this
    /// calls Stripe's real Refund API first, so a card is actually credited, not just the books - see
    /// <c>IPaymentService.ReversePaymentAsync</c>'s own remarks.
    /// </summary>
    /// <param name="amount">How much to reverse, or omit to reverse everything still live on this
    /// payment.</param>
    [Post("/payments/{paymentId}/reverse")]
    Task ReversePaymentAsync(Guid paymentId, PaymentStatus status, decimal? amount = null);

    /// <summary>Grants value back against an invoice without money moving.</summary>
    [Post("/credit-notes")]
    Task<InvoiceDto> IssueCreditNoteAsync([Body] IssueCreditNoteRequestDto request);

    /// <summary>Marks an invoice as never having been owed.</summary>
    [Post("/invoices/void")]
    Task<InvoiceDto> VoidInvoiceAsync([Body] CloseInvoiceRequestDto request);

    /// <summary>Gives up on a debt that was genuinely owed.</summary>
    [Post("/invoices/write-off")]
    Task<InvoiceDto> WriteOffInvoiceAsync([Body] CloseInvoiceRequestDto request);

    /// <summary>The chargeback packet for one disputed payment.</summary>
    [Get("/payments/{paymentId}/chargeback-evidence")]
    Task<ChargebackEvidenceDto> GetChargebackEvidenceAsync(Guid paymentId);

    /// <summary>Submits the chargeback packet to Stripe as the dispute's formal response - see
    /// <c>IStripeDisputeService.SubmitEvidenceAsync</c>'s own remarks: one-shot, not a draft.</summary>
    [Post("/payments/{paymentId}/chargeback-evidence/submit")]
    Task SubmitChargebackEvidenceAsync(Guid paymentId);
}
