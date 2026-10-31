using EthioServe.Application.Common;

namespace EthioServe.Application.Reviews;

/// <summary>The customer can supply only their rating and optional feedback; ownership comes from the access token.</summary>
public sealed record CreateReviewRequest(int Rating, string? Comment);

/// <summary>Public review data deliberately omits the customer's identity and private request details.</summary>
public sealed record ProviderReviewDto(Guid Id, int Rating, string? Comment, DateTimeOffset CreatedAt);

public sealed class ProviderReviewQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IReviewService
{
    Task<ProviderReviewDto> CreateAsync(Guid requestId, CreateReviewRequest request, CancellationToken ct = default);

    Task<PagedResult<ProviderReviewDto>> ListPublicAsync(
        Guid providerId, ProviderReviewQuery query, CancellationToken ct = default);
}
