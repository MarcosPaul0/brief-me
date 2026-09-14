namespace BriefMe.Web.Contracts.Users;

public sealed record UpdateUserRequest(
    string FullName,
    string Email,
    IReadOnlyCollection<Guid> RoleIds);
