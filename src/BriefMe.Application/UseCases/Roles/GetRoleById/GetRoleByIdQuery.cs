using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Roles.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IQuery<RoleDto>;
