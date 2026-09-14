using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Auth.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtService jwtService,
    IRefreshTokenService refreshTokenService) : ICommandHandler<LoginCommand, AuthenticationDto>
{
    public async Task<Result<AuthenticationDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidCredentials);

        var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
        if (user is null)
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidCredentials);

        if (user.Status != UserStatus.Active)
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidCredentials);

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Unauthorized<AuthenticationDto>(AuthErrors.InvalidCredentials);

        var (accessToken, expiresAt) = jwtService.GenerateAccessToken(user);
        var refreshToken = await refreshTokenService.GenerateAsync(user.Id, request.IpAddress, cancellationToken);

        return Result.Success(AuthenticationDto.FromUserAndToken(user, accessToken, expiresAt, refreshToken));
    }
}
