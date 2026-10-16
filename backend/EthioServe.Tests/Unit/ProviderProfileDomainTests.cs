using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class ProviderProfileDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, Action<ProviderProfile>> Actions = new()
    {
        ["Request"] = p => p.RequestVerification(),
        ["Approve"] = p => p.Approve(Now),
        ["Reject"] = p => p.Reject("Documents are unclear", Now),
        ["Suspend"] = p => p.Suspend("Customer complaints", Now),
        ["Reinstate"] = p => p.Reinstate(Now),
    };

    // Every move the state machine allows. Anything not listed here must be refused.
    private static readonly Dictionary<(VerificationStatus From, string Action), VerificationStatus> Allowed = new()
    {
        [(VerificationStatus.Unverified, "Request")] = VerificationStatus.Pending,
        [(VerificationStatus.Rejected, "Request")] = VerificationStatus.Pending,
        [(VerificationStatus.Pending, "Approve")] = VerificationStatus.Verified,
        [(VerificationStatus.Pending, "Reject")] = VerificationStatus.Rejected,
        [(VerificationStatus.Verified, "Suspend")] = VerificationStatus.Suspended,
        [(VerificationStatus.Suspended, "Reinstate")] = VerificationStatus.Verified,
    };

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var status in Enum.GetValues<VerificationStatus>())
        {
            foreach (var action in Actions.Keys)
            {
                yield return new object[] { status, action };
            }
        }
    }

    private static ProviderProfile NewProvider() =>
        new(Guid.NewGuid(), "Abebe Plumbing", "0911556677", 1, 1, description: "Experienced plumber.", address: "Bole");

    private static ProviderProfile InStatus(VerificationStatus status)
    {
        var provider = NewProvider();

        switch (status)
        {
            case VerificationStatus.Pending:
                provider.RequestVerification();
                break;
            case VerificationStatus.Verified:
                provider.RequestVerification();
                provider.Approve(Now);
                break;
            case VerificationStatus.Rejected:
                provider.RequestVerification();
                provider.Reject("Documents are unclear", Now);
                break;
            case VerificationStatus.Suspended:
                provider.RequestVerification();
                provider.Approve(Now);
                provider.Suspend("Customer complaints", Now);
                break;
        }

        return provider;
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Verification_moves_follow_the_state_machine_exactly(VerificationStatus from, string action)
    {
        var provider = InStatus(from);

        if (Allowed.TryGetValue((from, action), out var expected))
        {
            Actions[action](provider);
            Assert.Equal(expected, provider.VerificationStatus);
        }
        else
        {
            Assert.Throws<DomainException>(() => Actions[action](provider));
            Assert.Equal(from, provider.VerificationStatus);
        }
    }

    [Fact]
    public void New_providers_are_unverified_available_and_unrated()
    {
        var provider = NewProvider();

        Assert.Equal(VerificationStatus.Unverified, provider.VerificationStatus);
        Assert.True(provider.IsAvailable);
        Assert.Equal(0m, provider.AverageRating);
    }

    [Fact]
    public void Rejection_and_suspension_need_a_reason_and_keep_it()
    {
        var pending = InStatus(VerificationStatus.Pending);
        Assert.Throws<DomainException>(() => pending.Reject("   ", Now));
        Assert.Equal(VerificationStatus.Pending, pending.VerificationStatus);

        pending.Reject("Documents are unclear", Now);
        Assert.Equal("Documents are unclear", pending.VerificationNote);
        Assert.Equal(Now, pending.VerificationReviewedAt);

        var verified = InStatus(VerificationStatus.Verified);
        Assert.Throws<DomainException>(() => verified.Suspend("", Now));
        verified.Suspend("Customer complaints", Now);
        Assert.Equal("Customer complaints", verified.VerificationNote);
    }

    [Fact]
    public void A_reason_cannot_exceed_500_characters()
    {
        var pending = InStatus(VerificationStatus.Pending);

        Assert.Throws<DomainException>(() => pending.Reject(new string('x', 501), Now));
    }

    [Fact]
    public void Approval_and_reinstatement_clear_the_note_and_requesting_again_clears_a_rejection()
    {
        var suspended = InStatus(VerificationStatus.Suspended);
        suspended.Reinstate(Now);
        Assert.Null(suspended.VerificationNote);

        var rejected = InStatus(VerificationStatus.Rejected);
        rejected.RequestVerification();
        Assert.Null(rejected.VerificationNote);
        Assert.Equal(VerificationStatus.Pending, rejected.VerificationStatus);
    }

    [Fact]
    public void Details_can_be_updated()
    {
        var provider = NewProvider();

        provider.UpdateDetails("Kebede Plumbing", "  Fast and tidy work.  ", "0911000000", 12, 2, 3, 4, " Piassa ", 9.03m, 38.74m);

        Assert.Equal("Kebede Plumbing", provider.BusinessName);
        Assert.Equal("Fast and tidy work.", provider.Description);
        Assert.Equal("Piassa", provider.Address);
        Assert.Equal(12, provider.YearsOfExperience);
        Assert.Equal((2, 3, 4), (provider.RegionId, provider.CityId, provider.SubCityId));
        Assert.Equal(9.03m, provider.Latitude);
    }

    [Theory]
    [InlineData(-1, 9.03, 38.74)]
    [InlineData(61, 9.03, 38.74)]
    [InlineData(5, 91, 38.74)]
    [InlineData(5, 9.03, 181)]
    [InlineData(5, 9.03, null)]
    [InlineData(5, null, 38.74)]
    public void Invalid_details_are_rejected_and_change_nothing(int years, double? latitude, double? longitude)
    {
        var provider = NewProvider();

        Assert.Throws<DomainException>(() => provider.UpdateDetails(
            "Changed", "Changed", "0911000000", years, 2, 2, null, "Changed",
            (decimal?)latitude, (decimal?)longitude));

        Assert.Equal("Abebe Plumbing", provider.BusinessName);
    }

    [Fact]
    public void Availability_can_be_switched()
    {
        var provider = NewProvider();

        provider.SetAvailability(false);
        Assert.False(provider.IsAvailable);

        provider.SetAvailability(true);
        Assert.True(provider.IsAvailable);
    }

    [Fact]
    public void Offers_can_be_updated_and_reject_negative_prices()
    {
        var offer = new ProviderService(Guid.NewGuid(), Guid.NewGuid(), 300m, "First");

        offer.Update(450m, "Second", false);
        Assert.Equal((450m, "Second", false), (offer.Price, offer.Description, offer.IsActive));

        Assert.Throws<DomainException>(() => offer.Update(-1m, null, true));
        Assert.Equal(450m, offer.Price);
    }
}