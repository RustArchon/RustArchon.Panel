// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Components.Shared.Reporting;

/// <summary>
/// One control in a report's parameter bar, and its representation in the page's query string.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Parameters live in the URL, and that is load-bearing rather than a convenience.</strong> It
/// makes a tuned report shareable and bookmarkable - "saved reports" is a browser bookmark rather than a
/// schema change - it makes browser back and forward work through a report someone has been narrowing
/// down, and it is the mechanism drill-down uses: drilling from a row is building another report's URL
/// and navigating to it.
/// </para>
/// <para>
/// Plain typed classes rather than a fluent builder or a definition DSL. A report page states its
/// parameters as a list of these and reads their properties directly, so a mistyped parameter is a
/// compile error rather than an empty grid.
/// </para>
/// <para>
/// Adding a new kind of parameter is a subclass here plus a branch in <see cref="ReportPage"/>'s
/// parameter bar. Two exist so far - a date window and a dropdown - because those are the two anything
/// uses. A numeric threshold ("balance over") is the next one, and arrives with the collections reports.
/// </para>
/// </remarks>
public abstract class ReportParameter(string key, string label)
{
    /// <summary>Identifies this parameter, and forms the query-string key(s) it reads and writes.</summary>
    public string Key { get; } = key;

    /// <summary>What the parameter bar calls it.</summary>
    public string Label { get; } = label;

    /// <summary>This parameter's current value as query-string entries, for building the page URL.</summary>
    public abstract IEnumerable<KeyValuePair<string, object?>> ToQuery();

    /// <summary>
    /// Sets this parameter from the page's query string. Values that are absent or unparseable leave the
    /// current value alone, so a hand-edited or truncated URL degrades to the default rather than to
    /// something nonsensical like a zero date.
    /// </summary>
    public abstract void ReadFrom(IReadOnlyDictionary<string, string> query);
}

/// <summary>A relative window offered as a one-click preset - "Next 30 days", "Overdue".</summary>
/// <param name="Label">Button text.</param>
/// <param name="FromDays">Start of the window in whole days from today; negative is in the past.</param>
/// <param name="ToDays">End of the window in whole days from today, inclusive.</param>
public sealed record DateRangePreset(string Label, int FromDays, int ToDays);

/// <summary>
/// A from/to date window, with optional relative presets.
/// </summary>
/// <remarks>
/// <para>
/// Occupies two query-string keys, <c>{Key}From</c> and <c>{Key}To</c>, rather than encoding a range
/// into one - two plain dates survive being read, edited and pasted by a person, which a packed range
/// does not.
/// </para>
/// <para>
/// <strong>Dates here are UTC.</strong> The renewal instants they filter are stored in UTC, and this is
/// a Blazor Server app, so "local" would mean the server's timezone rather than the reader's - a
/// distinction that would quietly move the boundary of every window. Both ends are inclusive whole days.
/// </para>
/// </remarks>
public sealed class DateRangeParameter(
    string key,
    string label,
    DateOnly from,
    DateOnly to,
    IReadOnlyList<DateRangePreset>? presets = null)
    : ReportParameter(key, label)
{
    public DateOnly From { get; set; } = from;
    public DateOnly To { get; set; } = to;

    public IReadOnlyList<DateRangePreset> Presets { get; } = presets ?? [];

    public string FromKey => $"{Key}From";
    public string ToKey => $"{Key}To";

    /// <summary>Today, in the same UTC terms the rest of this parameter uses.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Whether <paramref name="preset"/> describes the window currently selected.</summary>
    public bool IsActive(DateRangePreset preset) =>
        From == Today.AddDays(preset.FromDays) && To == Today.AddDays(preset.ToDays);

    public void Apply(DateRangePreset preset)
    {
        From = Today.AddDays(preset.FromDays);
        To = Today.AddDays(preset.ToDays);
    }

    public override IEnumerable<KeyValuePair<string, object?>> ToQuery() =>
    [
        new(FromKey, Format(From)),
        new(ToKey, Format(To))
    ];

    public override void ReadFrom(IReadOnlyDictionary<string, string> query)
    {
        if (query.TryGetValue(FromKey, out var rawFrom) && TryParse(rawFrom, out var parsedFrom))
        {
            From = parsedFrom;
        }

        if (query.TryGetValue(ToKey, out var rawTo) && TryParse(rawTo, out var parsedTo))
        {
            To = parsedTo;
        }
    }

    /// <summary>The wire and <c>&lt;input type="date"&gt;</c> format - the same one in both directions.</summary>
    public static string Format(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool TryParse(string? raw, out DateOnly value) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
}

/// <summary>
/// A single-choice dropdown filter, with an always-present "all" option.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Options"/> is settable rather than fixed at construction because the choices usually come
/// from the API - the plan list, in every case so far. The page constructs the parameter with an empty
/// list so the bar can render immediately, then fills it in once the options arrive.
/// </para>
/// <para>
/// "All" is <c>null</c>, and a null value writes no query-string entry at all rather than an empty one.
/// That keeps the unfiltered URL clean, and means an absent key and an explicitly-empty one mean the
/// same thing - which matters because <see cref="ReadFrom"/> has to reset to "all" when someone
/// navigates back to a URL that didn't carry the filter.
/// </para>
/// </remarks>
public sealed class SelectParameter(string key, string label, string allLabel = "All")
    : ReportParameter(key, label)
{
    /// <summary>The selected option's value, or <c>null</c> for "all".</summary>
    public string? Value { get; set; }

    public IReadOnlyList<ReportFilterOptionDto> Options { get; set; } = [];

    /// <summary>What the "no filter" choice is called - "All plans", "Any status".</summary>
    public string AllLabel { get; } = allLabel;

    /// <summary>The chosen option, or <c>null</c> when the filter is off or the value is stale.</summary>
    public ReportFilterOptionDto? Selected =>
        Value is null ? null : Options.FirstOrDefault(o => o.Value == Value);

    public override IEnumerable<KeyValuePair<string, object?>> ToQuery() =>
        [new(Key, string.IsNullOrEmpty(Value) ? null : Value)];

    // Unlike a date, an absent key here is meaningful rather than missing: it is "all". Leaving the
    // previous selection in place would make Back leave a filter applied that the URL doesn't mention.
    public override void ReadFrom(IReadOnlyDictionary<string, string> query) =>
        Value = query.TryGetValue(Key, out var raw) && !string.IsNullOrEmpty(raw) ? raw : null;
}

/// <summary>
/// A numeric threshold - "balance over", "more than N days".
/// </summary>
/// <remarks>
/// Always invariant on the wire, whatever the reader's locale: a URL carrying <c>50.00</c> has to mean
/// the same thing when it is pasted into a chat and opened by somebody whose machine writes decimals
/// with a comma.
/// </remarks>
public sealed class DecimalParameter(string key, string label, decimal value = 0m, string? prefix = null)
    : ReportParameter(key, label)
{
    public decimal Value { get; set; } = value;

    /// <summary>Optional symbol shown before the input - a currency sign, usually.</summary>
    public string? Prefix { get; } = prefix;

    public override IEnumerable<KeyValuePair<string, object?>> ToQuery() =>
        // Zero means "no threshold", and writing it into the URL would only add noise to the common case.
        [new(Key, Value == 0m ? null : Value.ToString(CultureInfo.InvariantCulture))];

    public override void ReadFrom(IReadOnlyDictionary<string, string> query) =>
        Value = query.TryGetValue(Key, out var raw)
                && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
}
