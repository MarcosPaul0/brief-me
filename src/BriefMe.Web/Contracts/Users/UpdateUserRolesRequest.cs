namespace BriefMe.Web.Contracts.Users;

public sealed record UpdateUserRolesRequest(IReadOnlyCollection<Guid> RoleIds);
