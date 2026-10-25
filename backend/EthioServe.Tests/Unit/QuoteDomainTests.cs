using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class QuoteDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidUntil = Now.AddDays(7);
    private static readonly DateTimeOffset AfterExpiry = ValidUntil.AddHours(1);

    private static readonly Dictionary<string, Action<Quote>> Actions = new()
    {
        ["Accept"] = quote => quote.Accept(Now),
        ["Reject"] = quote => quote.Reject(),
        ["Withdraw"] = quote => quote.Withdraw(),
        ["Expire"] = quote => quote.Expire(AfterExpiry),
    };

    private static readonly Dictionary<string, QuoteStatus> Results = new()
    {
        ["Accept"] = QuoteStatus.Accepted,
        ["Reject"] = QuoteStatus.Rejected,
        ["Withdraw"] = QuoteStatus.Cancelled,
        ["Expire"] = QuoteStatus.Expired,
    };

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var status in Enum.GetValues<QuoteStatus>())
        {
            foreach (var action in Actions.Keys)
            {
                yield return new object[] { status, action };
            }
        }
    }

    private static Quote NewQuote() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 500m, "Parts and labour included.", 120, ValidUntil);

    private static Quote InStatus(QuoteStatus status)
    {
        var quote = NewQuote();

        switch (status)
        {
            case QuoteStatus.Accepted:
                quote.Accept(Now);
                break;
            case QuoteStatus.Rejected:
                quote.Reject();
                break;
            case QuoteStatus.Cancelled:
                quote.Withdraw();
                break;
            case QuoteStatus.Expired:
                quote.Expire(AfterExpiry);
                break;
        }

        return quote;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Only_a_pending_quote_can_change_status(QuoteStatus from, string action)
    {
        var quote = InStatus(from);

        if (from == QuoteStatus.Pending)
        {
            Actions[action](quote);

            Assert.Equal(Results[action], quote.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => Actions[action](quote));
            Assert.Equal(from, quote.Status);
        }
    }

    [Fact]
    public void A_new_quote_is_pending()
    {
        Assert.Equal(QuoteStatus.Pending, NewQuote().Status);
    }

    [Fact]
    public void A_quote_cannot_be_accepted_once_its_deadline_has_passed()
    {
        var quote = NewQuote();

        Assert.Throws<DomainException>(() => quote.Accept(AfterExpiry));
        Assert.Throws<DomainException>(() => quote.Accept(ValidUntil)); // exactly at the deadline counts as expired
        Assert.Equal(QuoteStatus.Pending, quote.Status);
    }

    [Fact]
    public void A_quote_that_is_still_valid_cannot_be_expired_but_one_at_its_deadline_can()
    {
        var early = NewQuote();
        var onTime = NewQuote();

        Assert.Throws<DomainException>(() => early.Expire(Now));
        Assert.Equal(QuoteStatus.Pending, early.Status);

        onTime.Expire(ValidUntil);

        Assert.Equal(QuoteStatus.Expired, onTime.Status);
    }

    [Fact]
    public void A_quote_is_open_only_while_pending_and_before_its_deadline()
    {
        var quote = NewQuote();

        Assert.True(quote.IsOpen(Now));
        Assert.False(quote.IsOpen(ValidUntil));
        Assert.False(quote.IsOpen(AfterExpiry));

        quote.Withdraw();

        Assert.False(quote.IsOpen(Now));
    }
}