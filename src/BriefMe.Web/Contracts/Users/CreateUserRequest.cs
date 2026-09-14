namespace BriefMe.Web.Contracts.Users;

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    IReadOnlyCollection<Guid> RoleIds);
