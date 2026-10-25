using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Quotes;

// The body deliberately has no id, status, provider or expiry date: the server decides those.
public sealed record SubmitQuoteRequest(decimal Amount, string Description, int EstimatedDurationMinutes, int ValidForDays);

/// <summary>The customer's details, shown to the chosen provider only after the customer accepts their quote.</summary>
public sealed record CustomerContactDto(string Name, string PhoneNumber, string Address);

/// <summary>A provider's own quote. The status is what a person should see (a quote past its deadline reads Expired).</summary>
public sealed record ProviderQuoteDto(
    Guid Id,
    Guid RequestId,
    string RequestTitle,
    string ServiceName,
    string CityName,
    ServiceRequestStatus RequestStatus,
    decimal Amount,
    string Description,
    int EstimatedDurationMinutes,
    DateTimeOffset ValidUntil,
    QuoteStatus Status,
    DateTimeOffset CreatedAt,
    CustomerContactDto? Contact);

/// <summary>A quote as the customer sees it. The provider's phone number appears only on the accepted quote.</summary>
public sealed record QuoteDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    decimal ProviderRating,
    int ProviderReviews,
    int ProviderYearsOfExperience,
    decimal Amount,
    string Description,
    int EstimatedDurationMinutes,
    DateTimeOffset ValidUntil,
    QuoteStatus Status,
    DateTimeOffset CreatedAt,
    string? ProviderPhone);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class ProviderQuoteQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IProviderQuoteService
{
    Task<ProviderQuoteDto> SubmitAsync(Guid requestId, SubmitQuoteRequest request, CancellationToken ct = default);

    Task<PagedResult<ProviderQuoteDto>> ListAsync(ProviderQuoteQuery query, CancellationToken ct = default);

    Task<ProviderQuoteDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<ProviderQuoteDto> WithdrawAsync(Guid id, CancellationToken ct = default);
}

public interface ICustomerQuoteService
{
    Task<IReadOnlyList<QuoteDto>> ListAsync(Guid requestId, CancellationToken ct = default);

    Task<QuoteDto> AcceptAsync(Guid requestId, Guid quoteId, CancellationToken ct = default);

    Task<QuoteDto> DeclineAsync(Guid requestId, Guid quoteId, CancellationToken ct = default);
}