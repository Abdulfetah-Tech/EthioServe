using EthioServe.Application.Attachments;

namespace EthioServe.Tests.Unit;

public sealed class ImageSnifferTests
{
    [Theory]
    [InlineData("FFD8FFE000104A464946", "image/jpeg", ".jpg")]
    [InlineData("89504E470D0A1A0A0000000D49484452", "image/png", ".png")]
    [InlineData("52494646A40000005745425056503820", "image/webp", ".webp")]
    public void Real_image_signatures_are_recognised(string hex, string contentType, string extension)
    {
        var kind = ImageSniffer.Detect(Convert.FromHexString(hex));

        Assert.NotNull(kind);
        Assert.Equal(contentType, kind.ContentType);
        Assert.Equal(extension, kind.Extension);
    }

    [Theory]
    [InlineData("")]
    [InlineData("FFD8")]
    [InlineData("89504E47")]
    [InlineData("5249464600")]
    [InlineData("474946383961")]
    [InlineData("255044462D312E37")]
    [InlineData("4D5A9000030000000400")]
    [InlineData("3C7376672076696577426F783D")]
    [InlineData("52494646A400000057415645")]
    [InlineData("68656C6C6F20776F726C64")]
    public void Anything_else_is_refused(string hex)
    {
        Assert.Null(ImageSniffer.Detect(Convert.FromHexString(hex)));
    }

    [Fact]
    public void The_riff_container_must_say_webp_not_wave()
    {
        var wave = Convert.FromHexString("52494646A400000057415645666D7420");
        var webp = Convert.FromHexString("52494646A40000005745425056503820");

        Assert.Null(ImageSniffer.Detect(wave));
        Assert.NotNull(ImageSniffer.Detect(webp));
    }
}