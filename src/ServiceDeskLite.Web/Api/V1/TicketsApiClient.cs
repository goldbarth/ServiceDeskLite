using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.WebUtilities;

using ServiceDeskLite.Contracts.V1.Agents;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Api.V1;

public sealed class TicketsApiClient : ITicketsApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _sendOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public TicketsApiClient(HttpClient http)
    {
        _http = http;
    }

    // -----------------------------
    // Public API
    // -----------------------------

    public async Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
        SearchTicketsRequest request,
        CancellationToken ct = default)
    {
        var queryParams = new List<KeyValuePair<string, string?>>
        {
            new("page", request.Page.ToString()),
            new("pageSize", request.PageSize.ToString()),
        };

        if (request.SortField is not null)
            queryParams.Add(new("sortField", request.SortField.ToString()));

        if (request.SortDirection is not null)
            queryParams.Add(new("sortDirection", request.SortDirection.ToString()));

        if (!string.IsNullOrWhiteSpace(request.Q))
            queryParams.Add(new("q", request.Q));

        if (request.Statuses is { Length: > 0 })
            queryParams.AddRange(request.Statuses.Select(s => new KeyValuePair<string, string?>("statuses", s.ToString())));

        if (request.Priorities is { Length: > 0 })
            queryParams.AddRange(request.Priorities.Select(p => new KeyValuePair<string, string?>("priorities", p.ToString())));

        if (!string.IsNullOrWhiteSpace(request.Assignee))
            queryParams.Add(new("assignee", request.Assignee));

        if (request.Unassigned == true)
            queryParams.Add(new("unassigned", "true"));

        if (request.Overdue == true)
            queryParams.Add(new("overdue", "true"));

        var url = QueryHelpers.AddQueryString("api/v1/tickets", queryParams);
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

        return await SendAsync<PagedResponse<TicketListItemResponse>>(httpRequest, ct);
    }

    public async Task<ApiResult<IReadOnlyList<AgentResponse>>> GetAgentsAsync(
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "api/v1/agents");
        return await SendAsync<IReadOnlyList<AgentResponse>>(httpRequest, ct);
    }

    public async Task<ApiResult<TicketResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/tickets/{id}");

        return await SendAsync<TicketResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<CreateTicketResponse>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v1/tickets")
        {
            Content = JsonContent.Create(request)
        };

        return await SendAsync<CreateTicketResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<TicketResponse>> UpdateAsync(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"api/v1/tickets/{id}")
        {
            Content = JsonContent.Create(request, options: _sendOptions)
        };

        return await SendAsync<TicketResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<TicketResponse>> ChangeStatusAsync(
        Guid id,
        ChangeTicketStatusRequest request,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/tickets/{id}/status")
        {
            Content = JsonContent.Create(request, options: _sendOptions)
        };

        return await SendAsync<TicketResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<TicketResponse>> AssignAsync(
        Guid id,
        AssignTicketRequest request,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/tickets/{id}/assign")
        {
            Content = JsonContent.Create(request, options: _sendOptions)
        };

        return await SendAsync<TicketResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<CommentResponse>> AddCommentAsync(
        Guid id,
        AddCommentRequest request,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v1/tickets/{id}/comments")
        {
            Content = JsonContent.Create(request, options: _sendOptions)
        };

        return await SendAsync<CommentResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<IReadOnlyList<AuditEventResponse>>> GetAuditEventsAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/tickets/{id}/audit-events");

        return await SendAsync<IReadOnlyList<AuditEventResponse>>(httpRequest, ct);
    }

    public async Task<ApiResult<DashboardSummaryResponse>> GetDashboardSummaryAsync(
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "api/v1/dashboard/summary");
        return await SendAsync<DashboardSummaryResponse>(httpRequest, ct);
    }

    public async Task<ApiResult<AiDashboardResponse>> GetAiDashboardAsync(
        CancellationToken ct = default)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "api/v1/dashboard/ai");
        return await SendAsync<AiDashboardResponse>(httpRequest, ct);
    }

    // -----------------------------
    // Central Send Logic
    // -----------------------------

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        try
        {
            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content
                    .ReadFromJsonAsync<T>(_jsonOptions, ct);

                if (data is null)
                    return ApiResult<T>.Failure(
                        CreateUnexpectedBodyError(response.StatusCode));

                return ApiResult<T>.Success(data);
            }

            return await CreateFailureResult<T>(response, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ApiResult<T>.Failure(new ApiError
            {
                Status = 0,
                Title = "Network error",
                Detail = ex.Message
            });
        }
    }

    // -----------------------------
    // Failure Handling
    // -----------------------------

    private async Task<ApiResult<T>> CreateFailureResult<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetailsDto>(_jsonOptions, ct);

            if (problem is null)
                return ApiResult<T>.Failure(
                    CreateUnexpectedBodyError(response.StatusCode));

            return ApiResult<T>.Failure(
                MapProblem(problem, (int)response.StatusCode));
        }
        catch
        {
            return ApiResult<T>.Failure(
                CreateUnexpectedBodyError(response.StatusCode));
        }
    }

    private static ApiError MapProblem(
        ProblemDetailsDto problem,
        int status)
    {
        string? code = null;
        string? errorType = null;
        string? traceId = null;
        Dictionary<string, object>? meta = null;

        if (problem.Extensions is not null)
        {
            if (problem.Extensions.TryGetValue("code", out var c))
                code = c.GetString();

            if (problem.Extensions.TryGetValue("errorType", out var e))
                errorType = e.GetString();

            if (problem.Extensions.TryGetValue("traceId", out var t))
                traceId = t.GetString();

            if (problem.Extensions.TryGetValue("meta", out var m))
            {
                try
                {
                    meta = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        m.GetRawText());
                }
                catch
                {
                    meta = null;
                }
            }
        }

        return new ApiError
        {
            Status = status,
            Title = problem.Title,
            Detail = problem.Detail,
            Code = code,
            ErrorType = errorType,
            TraceId = traceId,
            Meta = meta
        };
    }

    private static ApiError CreateUnexpectedBodyError(
        HttpStatusCode statusCode)
    {
        return new ApiError
        {
            Status = (int)statusCode,
            Title = "Invalid server response",
            Detail = "The server response could not be parsed."
        };
    }
}
