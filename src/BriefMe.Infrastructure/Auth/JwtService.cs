using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BriefMe.Application.Abstractions;
using BriefMe.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BriefMe.Infrastructure.Auth;

public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;

    public JwtService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey deve ter ao menos 32 bytes (256 bits) para assinatura HMAC-SHA256.");

        _signingCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var roles = user.UserRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!);

        var permissionCodes = new HashSet<string>();
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));

            foreach (var permission in role.RolePermissions.Where(rp => rp.Permission is not null).Select(rp => rp.Permission!))
                permissionCodes.Add(permission.Code);
        }

        claims.AddRange(permissionCodes.Select(code => new Claim(CustomClaimTypes.Permission, code)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
