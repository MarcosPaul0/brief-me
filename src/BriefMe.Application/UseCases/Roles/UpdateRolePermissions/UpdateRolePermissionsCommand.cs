using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Roles.UpdateRolePermissions;

public sealed record UpdateRolePermissionsCommand(Guid RoleId, IReadOnlyCollection<Guid> PermissionIds) : ICommand<RoleDto>;
