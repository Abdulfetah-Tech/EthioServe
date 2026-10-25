namespace EthioServe.Application.Attachments;

public sealed record ImageKind(string ContentType, string Extension);

/// <summary>
/// Recognises JPEG, PNG and WebP from the first bytes of a file. The file name and the content type the browser
/// claims are never trusted.
/// </summary>
public static class ImageSniffer
{
    private static ReadOnlySpan<byte> PngSignature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static ImageKind? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new ImageKind("image/jpeg", ".jpg");
        }

        if (header.StartsWith(PngSignature))
        {
            return new ImageKind("image/png", ".png");
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return new ImageKind("image/webp", ".webp");
        }

        return null;
    }
}