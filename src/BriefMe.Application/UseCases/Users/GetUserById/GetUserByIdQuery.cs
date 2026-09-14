using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.Users;

namespace BriefMe.Application.UseCases.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto>;
