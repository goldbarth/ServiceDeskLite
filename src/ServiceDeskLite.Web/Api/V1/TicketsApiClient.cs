using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.WebUtilities;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Api.V1;

public sealed class TicketsApiClient : ITicketsApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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
        var url = QueryHelpers.AddQueryString(
            "api/v1/tickets",
            new Dictionary<string, string?>
            {
                ["page"] = request.Page.ToString(),
                ["pageSize"] = request.PageSize.ToString(),
                ["sortField"] = request.SortField.ToString(),
                ["sortDirection"] = request.SortDirection.ToString()
            });

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

        return await SendAsync<PagedResponse<TicketListItemResponse>>(httpRequest, ct);
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
