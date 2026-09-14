using BriefMe.Domain.Enums;

namespace BriefMe.Domain.Repositories;

public sealed record UserFilter(string? Search = null, UserStatus? Status = null, Guid? RoleId = null);
