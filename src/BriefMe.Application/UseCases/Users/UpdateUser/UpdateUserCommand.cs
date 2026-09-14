using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;

namespace BriefMe.Application.UseCases.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<Guid> RoleIds) : ICommand<UserDto>;
