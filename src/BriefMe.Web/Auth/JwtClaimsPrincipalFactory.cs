using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BriefMe.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Web.Auth;

public sealed class JwtClaimsPrincipalFactory(IOptions<JwtSettings> jwtSettingsOptions)
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    private readonly TokenValidationParameters _validationParameters =
        JwtTokenValidationParameters.Build(jwtSettingsOptions.Value);

    public bool TryCreatePrincipal(string accessToken, [NotNullWhen(true)] out ClaimsPrincipal? principal)
    {
        try
        {
            principal = Handler.ValidateToken(accessToken, _validationParameters, out _);
            return true;
        }
        catch (SecurityTokenException)
        {
            principal = null;
            return false;
        }
        catch (ArgumentException)
        {
            principal = null;
            return false;
        }
    }
}
