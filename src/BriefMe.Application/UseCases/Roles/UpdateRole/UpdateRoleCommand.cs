using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Roles.UpdateRole;

public sealed record UpdateRoleCommand(Guid RoleId, string Name, string? Description) : ICommand<RoleDto>;
