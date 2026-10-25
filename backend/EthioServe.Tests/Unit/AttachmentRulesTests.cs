using EthioServe.Application.Attachments;

namespace EthioServe.Tests.Unit;

public sealed class AttachmentRulesTests
{
    [Fact]
    public void The_limits_are_five_photos_of_five_megabytes()
    {
        Assert.Equal(5, AttachmentRules.MaxFiles);
        Assert.Equal(5 * 1024 * 1024, AttachmentRules.MaxBytes);
    }

    [Theory]
    [InlineData("kitchen.png", "kitchen.png")]
    [InlineData("  spaced name.png  ", "spaced name.png")]
    [InlineData("C:\\Users\\Abebe\\kitchen.jpg", "kitchen.jpg")]
    [InlineData("../../etc/passwd.png", "passwd.png")]
    [InlineData("..\\..\\evil.png", "evil.png")]
    [InlineData("bad\u0000name\u0007.png", "badname.png")]
    [InlineData("evil\u202Egnp.exe", "evilgnp.exe")]
    public void File_names_are_cleaned_for_display(string input, string expected)
    {
        Assert.Equal(expected, AttachmentRules.CleanName(input, ".png"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("folder\\")]
    public void A_missing_name_becomes_photo_with_the_real_extension(string? input)
    {
        Assert.Equal("photo.webp", AttachmentRules.CleanName(input, ".webp"));
    }

    [Fact]
    public void Very_long_names_are_cut_to_255_characters()
    {
        Assert.Equal(255, AttachmentRules.CleanName(new string('x', 400) + ".png", ".png").Length);
    }
}