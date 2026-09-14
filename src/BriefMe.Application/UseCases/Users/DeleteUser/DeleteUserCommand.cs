using BriefMe.Application.Abstractions.Messaging;

namespace BriefMe.Application.UseCases.Users.DeleteUser;

public sealed record DeleteUserCommand(
    Guid UserId,
    string? IpAddress = null) : ICommand;
