using System.Net.Http.Json;
using BriefMe.Domain.Common;

namespace BriefMe.Web.Http;

/// <summary>
/// Shared read path for the typed Blazor API clients: performs the GET, maps a non-success response
/// through <see cref="ApiProblemMapper"/> and turns transport faults into a general failure, so no
/// screen ever sees an exception from a data call.
/// </summary>
internal static class ApiClientExtensions
{
    /// <typeparam name="TResult">What callers see — often an interface such as <c>IReadOnlyList&lt;T&gt;</c>.</typeparam>
    /// <typeparam name="TPayload">The concrete type the deserializer can materialize.</typeparam>
    public static async Task<Result<TResult>> GetResultAsync<TResult, TPayload>(
        this AuthenticatedApiClient apiClient,
        string requestUri,
        string fallbackMessage,
        CancellationToken cancellationToken)
        where TPayload : class, TResult
    {
        try
        {
            using var response = await apiClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<TResult>(response, fallbackMessage, cancellationToken: cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<TPayload>(ApiProblemMapper.JsonOptions, cancellationToken);

            return payload is null
                ? Result.Failure<TResult>(fallbackMessage)
                : Result.Success<TResult>(payload);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<TResult>(fallbackMessage);
        }
    }
}
