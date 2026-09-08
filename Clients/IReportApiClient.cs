// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for <c>ReportsController</c> - the platform-wide operational reports, gated
/// server-side by the <c>ViewReports</c> permission.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here takes a tenant id, and that is the distinction from
/// <see cref="ISubscriptionApiClient"/>: these read across every Organization on the platform. A caller
/// without the permission gets a 403, which the report pages surface as an access-denied panel rather
/// than an error.
/// </para>
/// <para>
/// One method per report. There is deliberately no generic "run report by name" entry point - a report
/// is an endpoint and a row type, and keeping that typed is what makes a wrong parameter a compile
/// error instead of an empty grid.
/// </para>
/// </remarks>
public interface IReportApiClient
{
    /// <summary>
    /// Organizations renewing between <paramref name="from"/> and <paramref name="to"/> inclusive.
    /// </summary>
    /// <remarks>
    /// The dates are formatted explicitly rather than left to <see cref="DateOnly"/>'s default
    /// <c>ToString</c>, which is culture-dependent - on a machine set to en-US it would send
    /// <c>09/06/2026</c>, which the API's <see cref="DateOnly"/> binder rejects.
    /// </remarks>
    [Get("/upcoming-renewals")]
    Task<ReportResult<UpcomingRenewalRowDto>> GetUpcomingRenewalsAsync(
        [Query(Format = "yyyy-MM-dd")] DateOnly from,
        [Query(Format = "yyyy-MM-dd")] DateOnly to,
        Guid? planId = null);

    /// <summary>Organizations created in the window, and whether each one ever added a server.</summary>
    [Get("/new-signups")]
    Task<ReportResult<NewSignupRowDto>> GetNewSignupsAsync(
        [Query(Format = "yyyy-MM-dd")] DateOnly from,
        [Query(Format = "yyyy-MM-dd")] DateOnly to);

    /// <summary>Every live subscription. No window - this is a snapshot of what's true now.</summary>
    [Get("/subscriptions")]
    Task<ReportResult<SubscriptionRegisterRowDto>> GetSubscriptionsAsync(Guid? planId = null);

    /// <summary>Plan moves that happened in the window.</summary>
    [Get("/plan-changes")]
    Task<ReportResult<PlanChangeRowDto>> GetPlanChangesAsync(
        [Query(Format = "yyyy-MM-dd")] DateOnly from,
        [Query(Format = "yyyy-MM-dd")] DateOnly to,
        Guid? planId = null);

    /// <summary>Accepted changes taking effect in the window.</summary>
    [Get("/scheduled-changes")]
    Task<ReportResult<ScheduledChangeRowDto>> GetScheduledChangesAsync(
        [Query(Format = "yyyy-MM-dd")] DateOnly from,
        [Query(Format = "yyyy-MM-dd")] DateOnly to);

    /// <summary>Open invoices with a balance, oldest due first.</summary>
    [Get("/receivables")]
    Task<ReportResult<ReceivableRowDto>> GetReceivablesAsync(decimal minOutstanding, bool overdueOnly);

    /// <summary>Organizations with money outstanding, worst first.</summary>
    [Get("/delinquent-accounts")]
    Task<ReportResult<DelinquentAccountRowDto>> GetDelinquentAccountsAsync(
        decimal minOutstanding, bool overdueOnly);

    /// <summary>The plans to offer in a report's plan dropdown.</summary>
    [Get("/plan-options")]
    Task<List<ReportFilterOptionDto>> GetPlanOptionsAsync();
}
