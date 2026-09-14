using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Domain.Enums;

namespace BriefMe.Application.UseCases.Users.ChangeUserStatus;

public sealed record ChangeUserStatusCommand(
    Guid UserId,
    UserStatus Status,
    string? IpAddress = null) : ICommand;
