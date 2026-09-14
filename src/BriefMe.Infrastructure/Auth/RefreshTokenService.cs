using System.Security.Cryptography;
using System.Text;
using BriefMe.Application.Abstractions;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Auth;

public sealed class RefreshTokenService(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IOptions<RefreshTokenSettings> options) : IRefreshTokenService
{
    private const string InvalidTokenMessage = "Refresh token inválido, expirado ou revogado.";
    private const int TokenSizeInBytes = 64;

    private readonly RefreshTokenSettings _settings = options.Value;

    public async Task<string> GenerateAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var plainToken = GenerateOpaqueToken();
        var refreshToken = RefreshToken.Create(Hash(plainToken), userId, ExpiresAt(), ipAddress);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return plainToken;
    }

    public async Task<Result<RotatedRefreshToken>> RotateAsync(string currentToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var current = await refreshTokenRepository.GetByTokenHashAsync(Hash(currentToken), cancellationToken);
        if (current is null)
            return Result.Failure<RotatedRefreshToken>(InvalidTokenMessage);

        if (current.IsRevoked)
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(current.UserId, ipAddress, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            return Result.Failure<RotatedRefreshToken>(InvalidTokenMessage);
        }

        if (current.IsExpired)
            return Result.Failure<RotatedRefreshToken>(InvalidTokenMessage);

        var newPlainToken = GenerateOpaqueToken();
        var newHash = Hash(newPlainToken);
        var replacement = RefreshToken.Create(newHash, current.UserId, ExpiresAt(), ipAddress);

        current.Revoke(ipAddress, newHash);
        await refreshTokenRepository.AddAsync(replacement, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new RotatedRefreshToken(newPlainToken, current.UserId));
    }

    public async Task RevokeAsync(string token, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var existing = await refreshTokenRepository.GetByTokenHashAsync(Hash(token), cancellationToken);
        if (existing is null || existing.IsRevoked)
            return;

        existing.Revoke(ipAddress);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    private DateTime ExpiresAt() => DateTime.UtcNow.AddDays(_settings.ExpirationDays);

    private static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
