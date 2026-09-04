// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RustArchon.Panel.Services;

/// <summary>
/// Turns a failed API call into a message safe to show a user directly.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Refit.ApiException.Message"/> is HTTP status-code jargon a user has no use for (e.g.
/// <c>"Response status code does not indicate success: 400 (Bad Request)"</c>) - confirmed live as
/// exactly what a real plan-limit rejection looked like to a user before this existed:
/// <c>"Failed to save server: Response status code does not indicate success: 400 (Bad Request). —
/// Your plan (Stone) allows up to 1 server(s). Upgrade your plan to add more."</c>, when the second
/// sentence alone was the entire useful message. This type exists so every page shows just that
/// second sentence, not the first.
/// </para>
/// <para>
/// <see cref="Refit.ApiException.Content"/> is either plain text (one of this API's own hand-written
/// <c>BadRequest("...")</c>/<c>Conflict("...")</c> messages, already written to be user-facing - see
/// <c>RustServersController.Create</c>'s plan-limit check for an example) or, for a model-validation
/// failure (<c>BadRequest(ModelState)</c>), a JSON <c>ValidationProblemDetails</c> body - showing that
/// raw JSON would be exactly as unfriendly as the status-code text this exists to avoid, so it's
/// parsed into the actual field-error sentences instead.
/// </para>
/// <para>
/// Not a substitute for actually recording what went wrong - see this type's own follow-up note in
/// the commit that added it: persisting failures somewhere reviewable (a logging/APM library writing
/// to EF, or similar) is real, separate work this doesn't attempt. This only ever governs what a user
/// sees.
/// </para>
/// </remarks>
public static class ApiErrorMessage
{
    /// <param name="ex">The exception a failed API call threw.</param>
    /// <param name="action">
    /// A lowercase verb phrase describing what was being attempted (e.g. <c>"save server"</c>),
    /// used only for the generic "couldn't determine why" fallback - <c>"Failed to {action}. Please
    /// try again."</c>
    /// </param>
    public static string For(Exception ex, string action) => ex switch
    {
        Refit.ApiException apiEx => ForApiException(apiEx, action),
        _ => Fallback(action)
    };

    private static string ForApiException(Refit.ApiException ex, string action)
    {
        var content = ex.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return Fallback(action);
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return TryExtractValidationMessages(content) ?? Fallback(action);
        }

        // Plain text - already written to be shown to a user as-is.
        return content;
    }

    /// <summary>
    /// Reads an ASP.NET Core <c>ValidationProblemDetails</c> body's <c>errors</c> map and joins every
    /// field's messages into one sentence-per-error string - e.g. <c>"The Name field is required. The
    /// Host field is required."</c> - rather than showing the raw JSON.
    /// </summary>
    private static string? TryExtractValidationMessages(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("errors", out var errors)
                || errors.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var messages = new List<string>();
            foreach (var field in errors.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var message in field.Value.EnumerateArray())
                {
                    var text = message.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(text);
                    }
                }
            }

            return messages.Count > 0 ? string.Join(" ", messages) : null;
        }
        catch (JsonException)
        {
            // Some other JSON shape this doesn't know how to read - fall back rather than guess.
            return null;
        }
    }

    private static string Fallback(string action) => $"Failed to {action}. Please try again.";
}
