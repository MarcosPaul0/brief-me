using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Permissions.GetPermissions;

public sealed record GetPermissionsQuery : IQuery<IReadOnlyCollection<PermissionGroupDto>>;
