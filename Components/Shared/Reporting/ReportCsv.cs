// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RustArchon.Panel.Components.Shared.Reporting;

/// <summary>
/// Turns a report's rows and columns into CSV.
/// </summary>
/// <remarks>
/// <para>
/// CSV rather than XLSX because what matters is that the numbers arrive as numbers. An analyst is going
/// to finish the job in Excel either way, and a spreadsheet full of <c>"$1,234.00"</c> strings has to be
/// cleaned before it can be summed - which is why this writes
/// <see cref="ReportColumn{TRow}.Value"/> and never the display text.
/// </para>
/// </remarks>
public static class ReportCsv
{
    /// <summary>
    /// Builds the CSV text for <paramref name="rows"/>, in the order given, using only the exportable
    /// columns (see <see cref="ReportColumn{TRow}.Exportable"/>).
    /// </summary>
    public static string Build<TRow>(IEnumerable<TRow> rows, IEnumerable<ReportColumn<TRow>> columns)
    {
        var exported = columns.Where(c => c.Exportable).ToList();
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(',', exported.Select(c => Escape(c.Header))));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', exported.Select(c => Escape(Format(c.Value!(row))))));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a raw value in a form a spreadsheet will read back as the same type - invariant
    /// throughout, so a file generated on one machine parses identically on another.
    /// </summary>
    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        // Space-separated rather than ISO 8601's "T": Excel parses this straight to a datetime, and
        // reports are read in Excel far more often than by anything that wants a strict timestamp.
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// Quotes a field where CSV requires it, and defuses the leading characters that make a spreadsheet
    /// treat a cell as a formula.
    /// </summary>
    /// <remarks>
    /// The formula guard is not paranoia: Organization names are typed by users, and a cell opening with
    /// <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c> or a tab is executed by Excel when the file is opened. A
    /// leading apostrophe forces it to text. The <see cref="IsNumeric"/> exemption is what keeps
    /// <c>-12.50</c> exporting as a number rather than as the text <c>'-12.50</c>.
    /// </remarks>
    private static string Escape(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]) && !IsNumeric(value))
        {
            value = "'" + value;
        }

        var needsQuotes = value.Contains(',')
                          || value.Contains('"')
                          || value.Contains('\n')
                          || value.Contains('\r')
                          || value.StartsWith(' ')
                          || value.EndsWith(' ');

        return needsQuotes ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    }

    private static bool IsNumeric(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
}
