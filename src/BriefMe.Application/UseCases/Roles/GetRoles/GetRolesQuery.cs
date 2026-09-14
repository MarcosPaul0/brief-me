using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Roles.GetRoles;

public sealed record GetRolesQuery : IQuery<IReadOnlyCollection<RoleListItemDto>>;
