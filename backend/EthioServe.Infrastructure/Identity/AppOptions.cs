using System.ComponentModel.DataAnnotations;

namespace EthioServe.Infrastructure.Identity;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Where the Angular app lives. Used to build links inside emails.</summary>
    [Required, Url]
    public string FrontendBaseUrl { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int PasswordResetTokenMinutes { get; set; } = 60;

    /// <summary>Development only: print full email bodies (including reset links) to the log.</summary>
    public bool LogEmailBodies { get; set; }
}