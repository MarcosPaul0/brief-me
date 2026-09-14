using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;

namespace BriefMe.Application.UseCases.Roles.CreateRole;

public sealed record CreateRoleCommand(string Name, string? Description) : ICommand<RoleDto>;
