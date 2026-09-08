// Copyright ©2026 Scott Blomfield

using System;
using Microsoft.AspNetCore.Components;

namespace RustArchon.Panel.Components.Shared.Reporting;

/// <summary>Horizontal alignment of a report column's cells and header.</summary>
public enum ReportAlign
{
    Left,
    Right,
    Center
}

/// <summary>
/// One column of a <see cref="ReportGrid{TRow}"/> - how to get the value out of a row, how to show it,
/// and where clicking it goes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The raw value and its display form are separate on purpose.</strong> <see cref="Value"/> is
/// what sorting and CSV export both operate on; <see cref="Display"/> only affects what appears on
/// screen. Collapsing the two would mean sorting a money column alphabetically ("$1,000" before "$9")
/// and exporting text an analyst then has to strip currency symbols out of before Excel will add it up.
/// </para>
/// <para>
/// <strong>A column with no <see cref="Value"/> is screen furniture</strong> - an actions cell, a status
/// pill with no underlying figure - and is left out of the export entirely rather than exported as a
/// blank column. That is the whole rule; there is no separate flag to keep in sync with it.
/// </para>
/// </remarks>
/// <typeparam name="TRow">The report's row type.</typeparam>
/// <param name="header">Column heading.</param>
/// <param name="value">
/// The raw value - used for sorting and export. Omit for a column that is purely presentational.
/// </param>
/// <param name="display">
/// Screen text, when it should differ from <paramref name="value"/> - currency and date formatting,
/// mostly. Falls back to the raw value's own <c>ToString()</c>.
/// </param>
/// <param name="template">
/// Screen markup, for cells that are more than text - a coloured badge, an icon. Wins over
/// <paramref name="display"/>. Has no effect on the export, which still uses <paramref name="value"/>.
/// </param>
/// <param name="drillTo">
/// The URL this cell navigates to, or <c>null</c> for rows that have nowhere to go. Deliberately per
/// column rather than per row: a row that navigates on any click makes its own action buttons hazardous
/// to press, so the link belongs on the column that names the thing being drilled into.
/// </param>
/// <param name="align">Cell alignment. Numeric columns generally want <see cref="ReportAlign.Right"/>.</param>
/// <param name="sortable">Defaults to sortable whenever <paramref name="value"/> is supplied.</param>
/// <param name="width">Optional CSS width for the column, e.g. <c>"12rem"</c>.</param>
public sealed class ReportColumn<TRow>(
    string header,
    Func<TRow, object?>? value = null,
    Func<TRow, string>? display = null,
    RenderFragment<TRow>? template = null,
    Func<TRow, string?>? drillTo = null,
    ReportAlign align = ReportAlign.Left,
    bool? sortable = null,
    string? width = null)
{
    public string Header { get; } = header;
    public Func<TRow, object?>? Value { get; } = value;
    public Func<TRow, string>? Display { get; } = display;
    public RenderFragment<TRow>? Template { get; } = template;
    public Func<TRow, string?>? DrillTo { get; } = drillTo;
    public ReportAlign Align { get; } = align;
    public string? Width { get; } = width;

    /// <summary>Whether the grid offers this column as a sort key.</summary>
    public bool Sortable { get; } = sortable ?? value is not null;

    /// <summary>Whether this column appears in the CSV export. See this class's remarks.</summary>
    public bool Exportable => Value is not null;

    /// <summary>The plain text for a cell, ignoring <see cref="Template"/>.</summary>
    public string ScreenText(TRow row) =>
        Display is not null ? Display(row) : Value?.Invoke(row)?.ToString() ?? string.Empty;

    /// <summary>The Bootstrap alignment class for this column.</summary>
    public string AlignClass => Align switch
    {
        ReportAlign.Right => "text-end",
        ReportAlign.Center => "text-center",
        _ => "text-start"
    };
}
