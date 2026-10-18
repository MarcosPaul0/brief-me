using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;

namespace BriefMe.Application.UseCases.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenService refreshTokenService,
    IUserRepository userRepository,
    IJwtService jwtService) : ICommandHandler<RefreshTokenCommand, AuthenticationDto>
{
    public async Task<Result<AuthenticationDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var rotation = await refreshTokenService.RotateAsync(request.RefreshToken, request.IpAddress, cancellationToken);
        if (rotation.IsFailure)
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidRefreshToken);

        var user = await userRepository.GetByIdAsync(rotation.Value.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            await refreshTokenService.RevokeAsync(rotation.Value.Token, request.IpAddress, cancellationToken);
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidRefreshToken);
        }

        var (accessToken, expiresAt) = jwtService.GenerateAccessToken(user);

        return Result.Success(AuthenticationDto.FromUserAndToken(user, accessToken, expiresAt, rotation.Value.Token));
    }
}
