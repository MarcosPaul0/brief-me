using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Users;

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    UserRole Role);
