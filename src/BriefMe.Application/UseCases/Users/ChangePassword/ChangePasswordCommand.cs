using BriefMe.Application.Abstractions.Messaging;

namespace BriefMe.Application.UseCases.Users.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid UserId,
    string? CurrentPassword,
    string NewPassword,
    string? IpAddress = null) : ICommand;
