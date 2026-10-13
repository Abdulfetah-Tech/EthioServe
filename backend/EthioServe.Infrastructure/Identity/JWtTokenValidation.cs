using System.Text;
using EthioServe.Application.Common;
using Microsoft.IdentityModel.Tokens;

namespace EthioServe.Infrastructure.Identity;

public static class JwtTokenValidation
{
    /// <summary>The single definition of "a valid access token", used by the API and by the tests.</summary>
    public static TokenValidationParameters CreateParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = AppClaims.Subject,
        RoleClaimType = AppClaims.Role
    };
}