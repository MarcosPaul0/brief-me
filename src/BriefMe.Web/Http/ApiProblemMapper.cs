using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BriefMe.Domain.Common;

namespace BriefMe.Web.Http;

/// <summary>
/// Translates a non-success API response into a failed <see cref="Result{T}"/>, shared by the typed
/// Blazor API clients so every screen surfaces backend failures the same way. A validation
/// <c>ProblemDetails</c> (400 with an <c>errors</c> dict) becomes field-keyed <see cref="ValidationError"/>s;
/// the remaining statuses map onto the matching <see cref="ErrorType"/>.
/// </summary>
internal static class ApiProblemMapper
{
    // ASP.NET Core serializes controller responses in camelCase; bind them back onto PascalCase records.
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="conflictField">
    /// Form field a 409 is attributed to (a conflict carries no per-field dict). Null keeps it a general
    /// failure — used by screens where no single field owns the conflict.
    /// </param>
    public static async Task<Result<T>> MapFailureAsync<T>(
        HttpResponseMessage response,
        string fallbackMessage,
        string? conflictField = null,
        CancellationToken cancellationToken = default)
    {
        ApiProblem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Non-problem body (or empty) — fall back to the status-based mapping below.
        }
        catch (NotSupportedException)
        {
        }

        if (problem?.Errors is { Count: > 0 } errors)
        {
            var validationErrors = errors
                .SelectMany(pair => pair.Value.Select(message => new ValidationError(pair.Key, message)))
                .ToList();

            return Result.Failure<T>(validationErrors);
        }

        var detail = string.IsNullOrWhiteSpace(problem?.Detail) ? fallbackMessage : problem!.Detail!;

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict when conflictField is not null =>
                Result.Failure<T>([new ValidationError(conflictField, detail)]),
            HttpStatusCode.Conflict => Result.Conflict<T>(detail),
            HttpStatusCode.NotFound => Result.NotFound<T>(detail),
            HttpStatusCode.Forbidden => Result.Forbidden<T>(detail),
            HttpStatusCode.Unauthorized => Result.Unauthorized<T>(detail),
            _ => Result.Failure<T>(detail)
        };
    }

    private sealed record ApiProblem(int? Status, string? Detail, Dictionary<string, string[]>? Errors);
}
