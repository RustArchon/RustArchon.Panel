// Copyright ©2026 Scott Blomfield

namespace RustArchon.Panel.Components.Shared.Reporting;

/// <summary>
/// Which nav flyout a report belongs under - <see cref="Layout.NavMenu"/> and the three per-area
/// report landing pages (Pages/Customers/Reports.razor, Pages/Billing/Reports.razor,
/// Pages/Payments/Reports.razor) both key off this, so it exists once here rather than as a string
/// each of them would have to agree on.
/// </summary>
public enum ReportArea
{
    Customers,
    Billing,
    Payments
}

/// <summary>
/// One report, as it appears on its area's report landing page.
/// </summary>
/// <remarks>
/// A flat list of descriptors, not a registry - it renders a menu and nothing else. A report itself
/// is still a route, an endpoint and a row type; nothing here defines, parameterises or runs one. The
/// moment this starts carrying anything a report page reads back, it has turned into the report DSL
/// we deliberately didn't build.
/// </remarks>
public sealed record ReportLink(ReportArea Area, string Group, string Title, string Description, string Route, string Icon);

/// <summary>
/// Every report that exists, in one place - listing anything else here would turn a report landing
/// page into a roadmap.
/// </summary>
public static class ReportCatalog
{
    public static readonly ReportLink[] All =
    [
        new(ReportArea.Customers, "Growth",
            "New Signups",
            "Who registered over a window, and whether they ever provisioned a server. Signups that never activate were never customers.",
            "/Customers/Reports/NewSignups",
            "bi-person-plus-fill"),

        new(ReportArea.Customers, "Subscription lifecycle",
            "Subscriptions",
            "Every organization currently on a plan, with its monthly value. The plan mix and MRR come out of this one.",
            "/Customers/Reports/Subscriptions",
            "bi-people-fill"),

        new(ReportArea.Customers, "Subscription lifecycle",
            "Plan Changes",
            "Upgrades, downgrades and drops to a free plan over a window - including what each one did to monthly revenue.",
            "/Customers/Reports/PlanChanges",
            "bi-arrow-left-right"),

        new(ReportArea.Customers, "Subscription lifecycle",
            "Scheduled Changes",
            "Changes already accepted but not yet in force. Revenue that is going to move on a date nothing else shows you.",
            "/Customers/Reports/ScheduledChanges",
            "bi-clock-history"),

        new(ReportArea.Customers, "Subscription lifecycle",
            "Upcoming Renewals",
            "What bills in the next N days, for how much, and which organizations have a plan change landing at renewal.",
            "/Customers/Reports/UpcomingRenewals",
            "bi-calendar-check"),

        new(ReportArea.Customers, "Risk",
            "Discount Abuse Signals",
            "Organizations sharing a server or contact email where at least one has redeemed a discount - a flag to review, not a conclusion.",
            "/Customers/Reports/DiscountAbuse",
            "bi-flag"),

        new(ReportArea.Billing, "Collections",
            "Delinquent Accounts",
            "Who owes money and how long they've owed it, aged, with what each is worth per month if they stay.",
            "/Billing/Reports/DelinquentAccounts",
            "bi-exclamation-triangle-fill"),

        new(ReportArea.Billing, "Collections",
            "Receivables",
            "Every open invoice with a balance, oldest due first - the register the chase list is grouped from.",
            "/Billing/Reports/Receivables",
            "bi-cash-stack"),

        new(ReportArea.Payments, "Reconciliation",
            "Payment Ledger",
            "Every payment attempt, successful or failed - processor reconciliation against Stripe's own dashboard, and who's being declined.",
            "/Payments/Reports/PaymentLedger",
            "bi-credit-card")
    ];

    public static IEnumerable<ReportLink> ForArea(ReportArea area) => All.Where(r => r.Area == area);
}
