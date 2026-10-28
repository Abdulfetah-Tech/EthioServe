using EthioServe.Application.Notifications;

namespace EthioServe.Tests.Unit;

public sealed class NotificationTextTests
{
    [Fact]
    public void Text_inside_the_limit_is_only_trimmed()
    {
        Assert.Equal("Hello", NotificationText.Title("  Hello  "));
        Assert.Equal("Hello", NotificationText.Message("  Hello  "));
    }

    [Fact]
    public void The_limits_match_the_database_columns()
    {
        Assert.Equal(150, NotificationText.MaxTitle);
        Assert.Equal(1000, NotificationText.MaxMessage);
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(151, 150)]
    [InlineData(5000, 150)]
    public void A_long_title_is_cut_to_exactly_the_limit_and_ends_with_dots(int length, int expected)
    {
        var result = NotificationText.Title(new string('x', length));

        Assert.Equal(expected, result.Length);

        if (length > 150)
        {
            Assert.EndsWith("...", result);
        }
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1001, 1000)]
    [InlineData(20000, 1000)]
    public void A_long_message_is_cut_to_exactly_the_limit_and_ends_with_dots(int length, int expected)
    {
        var result = NotificationText.Message(new string('x', length));

        Assert.Equal(expected, result.Length);

        if (length > 1000)
        {
            Assert.EndsWith("...", result);
        }
    }

    [Fact]
    public void Cutting_never_leaves_a_space_before_the_dots()
    {
        var result = NotificationText.Clip(new string('a', 6) + " " + new string('b', 20), 10);

        Assert.DoesNotContain(" ...", result);
        Assert.EndsWith("...", result);
        Assert.True(result.Length <= 10);
    }
}