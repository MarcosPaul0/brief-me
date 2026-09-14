using BriefMe.Application.Common.Dtos;
using BriefMe.Domain.Common;
using BriefMe.Web.Contracts.Common;
using BriefMe.Web.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace BriefMe.Web.ApiClients;

/// <summary>
/// Typed client for the in-app inbox (BRF-054). Sends through <see cref="AuthenticatedApiClient"/> (the
/// "Api" pipeline: bearer + refresh-on-401) and maps non-success responses via
/// <see cref="ApiProblemMapper"/>, so the bell surfaces backend failures the way every other screen does.
/// </summary>
/// <remarks>
/// Every method answers with a <c>Result</c> and none of them throws: the badge is refreshed by a timer, and
/// an exception escaping a background poll would fault the circuit over a transient network blip.
/// </remarks>
public sealed class NotificationsApiClient : INotificationsApiClient
{
    private const string LoadNotificationsFailureMessage = "Não foi possível carregar as notificações. Tente novamente.";
    private const string LoadUnreadCountFailureMessage = "Não foi possível verificar as notificações. Tente novamente.";
    private const string MarkAsReadFailureMessage = "Não foi possível marcar as notificações como lidas. Tente novamente.";

    private static readonly JsonSerializerOptions ResponseJsonOptions = ApiProblemMapper.JsonOptions;

    private readonly AuthenticatedApiClient _api;

    public NotificationsApiClient(AuthenticatedApiClient api) => _api = api;

    public async Task<Result<PagedResponse<NotificationDto>>> GetNotificationsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync(BuildPagedRequestUri("notifications", page, pageSize), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<PagedResponse<NotificationDto>>(response, LoadNotificationsFailureMessage, cancellationToken: cancellationToken);

            var notifications = await response.Content.ReadFromJsonAsync<PagedResponse<NotificationDto>>(ResponseJsonOptions, cancellationToken);

            return notifications is null
                ? Result.Failure<PagedResponse<NotificationDto>>(LoadNotificationsFailureMessage)
                : Result.Success(notifications);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PagedResponse<NotificationDto>>(LoadNotificationsFailureMessage);
        }
    }

    public async Task<Result<UnreadNotificationCountDto>> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _api.GetAsync("notifications/unread-count", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return await ApiProblemMapper.MapFailureAsync<UnreadNotificationCountDto>(response, LoadUnreadCountFailureMessage, cancellationToken: cancellationToken);

            var count = await response.Content.ReadFromJsonAsync<UnreadNotificationCountDto>(ResponseJsonOptions, cancellationToken);

            return count is null
                ? Result.Failure<UnreadNotificationCountDto>(LoadUnreadCountFailureMessage)
                : Result.Success(count);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<UnreadNotificationCountDto>(LoadUnreadCountFailureMessage);
        }
    }

    public async Task<Result> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // No body: the set to mark is "the caller's unread ones", which the API resolves from the token.
            using var response = await _api.PostAsync("notifications/read", cancellationToken);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : await ApiProblemMapper.MapFailureAsync(response, MarkAsReadFailureMessage, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result.Failure(MarkAsReadFailureMessage);
        }
    }

    /// <summary>Adds the paging pair to a route that takes no other parameter.</summary>
    private static string BuildPagedRequestUri(string route, int page, int pageSize) =>
        QueryHelpers.AddQueryString(route, new Dictionary<string, string?>
        {
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        });
}
