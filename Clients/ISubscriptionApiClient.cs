// Copyright ©2026 Scott Blomfield

using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for <c>SubscriptionController</c> - the caller's own Organization's plan and
/// term. Everything here is scoped server-side to whichever tenant the caller is currently in; nothing
/// takes a tenant id.
/// </summary>
/// <remarks>
/// Not an <see cref="JumpStart.Api.Clients.IApiClient{TDto,TCreateDto,TUpdateDto}"/>: a subscription
/// isn't a CRUD collection. There is exactly one per Organization, it's never created or deleted
/// through this API, and "changing" it is a domain operation with rules attached rather than a PUT of a
/// new representation.
/// </remarks>
public interface ISubscriptionApiClient
{
    /// <summary>The current plan, term, billing period and any change already scheduled.</summary>
    [Get("")]
    Task<SubscriptionDto> GetAsync();

    /// <summary>
    /// Every billing period this Organization has been charged for, newest first - including the
    /// prorated partial periods a mid-period change creates.
    /// </summary>
    [Get("/billing-history")]
    Task<List<BillingHistoryEntryDto>> GetBillingHistoryAsync();

    /// <summary>The plans available to move to, each flagged current/allowed.</summary>
    [Get("/plan-options")]
    Task<List<PlanOptionDto>> GetPlanOptionsAsync();

    /// <summary>
    /// What a change would do, without doing it - what happens today, what happens later, what it
    /// costs, and whether it's allowed. Always call this and show the result before
    /// <see cref="ChangeAsync"/>: part of a change can take effect months after the user accepts it.
    /// </summary>
    [Post("/quote")]
    Task<PlanChangeQuoteDto> QuoteAsync([Body] ChangePlanRequestDto request);

    /// <summary>Applies the change, returning the same quote describing what was done.</summary>
    [Post("/change")]
    Task<PlanChangeQuoteDto> ChangeAsync([Body] ChangePlanRequestDto request);

    /// <summary>Calls off a scheduled change that hasn't taken effect yet.</summary>
    [Delete("/scheduled-change")]
    Task CancelScheduledChangeAsync();
}
