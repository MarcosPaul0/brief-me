namespace BriefMe.Web.Contracts.Roles;

public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<Guid> PermissionIds);
