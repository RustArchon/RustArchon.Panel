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

    /// <summary>Undoes a payment - a refund or a chargeback.</summary>
    [Post("/payments/{paymentId}/reverse")]
    Task ReversePaymentAsync(Guid paymentId, PaymentStatus status);

    /// <summary>Grants value back against an invoice without money moving.</summary>
    [Post("/credit-notes")]
    Task<InvoiceDto> IssueCreditNoteAsync([Body] IssueCreditNoteRequestDto request);

    /// <summary>Marks an invoice as never having been owed.</summary>
    [Post("/invoices/void")]
    Task<InvoiceDto> VoidInvoiceAsync([Body] CloseInvoiceRequestDto request);

    /// <summary>Gives up on a debt that was genuinely owed.</summary>
    [Post("/invoices/write-off")]
    Task<InvoiceDto> WriteOffInvoiceAsync([Body] CloseInvoiceRequestDto request);
}
