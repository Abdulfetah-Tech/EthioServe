using System.Globalization;

namespace EthioServe.Application.Attachments;

public static class AttachmentRules
{
    public const int MaxFiles = 5;

    public const int MaxBytes = 5 * 1024 * 1024;

    private const int MaxFileNameLength = 255;

    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// The name shown to people. It is never used to store the file. Directory parts, control characters and invisible
    /// formatting characters (such as the right-to-left override used to fake extensions) are removed.
    /// </summary>
    public static string CleanName(string? fileName, string extension)
    {
        var name = fileName ?? string.Empty;

        var lastSeparator = name.LastIndexOfAny(PathSeparators);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        name = new string(name
            .Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format)
            .ToArray()).Trim();

        if (name.Length > MaxFileNameLength)
        {
            name = name[..MaxFileNameLength];
        }

        return name.Length == 0 ? "photo" + extension : name;
    }
}