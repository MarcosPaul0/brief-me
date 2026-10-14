using BriefMe.Domain.Enums;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;

namespace BriefMe.Application.UseCases.Users.CreateUser;

public sealed record CreateUserCommand(
    string FullName,
    string Email,
    string Password,
    UserRole Role) : ICommand<UserDto>;
