using BriefMe.Domain.Common;

namespace BriefMe.Application.Abstractions;

public sealed record RotatedRefreshToken(string Token, Guid UserId);

public interface IRefreshTokenService
{
    Task<string> GenerateAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<Result<RotatedRefreshToken>> RotateAsync(string currentToken, string? ipAddress, CancellationToken cancellationToken = default);

    Task RevokeAsync(string token, string? ipAddress, CancellationToken cancellationToken = default);
}
