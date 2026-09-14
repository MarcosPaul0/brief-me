using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Infrastructure.Auth;

public static class JwtTokenValidationParameters
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    public static TokenValidationParameters Build(JwtSettings settings) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
        ClockSkew = ClockSkew
    };
}
