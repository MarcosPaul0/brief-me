using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Errors;
using BriefMe.Domain.Common;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.UseCases.Users.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.NotFound(UserErrors.NotFound);

        if (!string.IsNullOrWhiteSpace(request.CurrentPassword)
            && !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure(UserErrors.IncorrectCurrentPassword);

        var newPasswordResult = Password.Create(request.NewPassword);
        if (newPasswordResult.IsFailure)
            return Result.Failure(newPasswordResult.Error!);

        if (passwordHasher.Verify(newPasswordResult.Value.Value, user.PasswordHash))
            return Result.Failure(UserErrors.SamePassword);

        user.ChangePassword(passwordHasher.Hash(newPasswordResult.Value.Value));

        await refreshTokenRepository.RevokeAllActiveForUserAsync(user.Id, request.IpAddress, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
