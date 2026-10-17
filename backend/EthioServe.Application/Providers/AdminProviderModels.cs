using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Providers;

public sealed record AdminProviderListItemDto(
    Guid Id,
    string BusinessName,
    string PhoneNumber,
    string CityName,
    VerificationStatus VerificationStatus,
    string? VerificationNote,
    decimal AverageRating,
    int TotalReviews,
    int ActiveServiceCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerificationReviewedAt);

public sealed record AdminProviderDetailDto(
    ProviderProfileDto Profile,
    Guid UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? VerificationReviewedAt,
    IReadOnlyList<ProviderOfferDto> Offers);

/// <summary>The reason an admin gives when rejecting or suspending a provider. The provider sees it.</summary>
public sealed record ReviewDecisionRequest(string Reason);

public sealed class AdminProviderQuery
{
    public VerificationStatus? Status { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IAdminProviderService
{
    Task<PagedResult<AdminProviderListItemDto>> SearchAsync(AdminProviderQuery query, CancellationToken ct = default);

    Task<AdminProviderDetailDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<AdminProviderDetailDto> ApproveAsync(Guid id, CancellationToken ct = default);

    Task<AdminProviderDetailDto> RejectAsync(Guid id, ReviewDecisionRequest request, CancellationToken ct = default);

    Task<AdminProviderDetailDto> SuspendAsync(Guid id, ReviewDecisionRequest request, CancellationToken ct = default);

    Task<AdminProviderDetailDto> ReinstateAsync(Guid id, CancellationToken ct = default);
}