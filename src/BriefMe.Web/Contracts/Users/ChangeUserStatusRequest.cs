using BriefMe.Domain.Enums;

namespace BriefMe.Web.Contracts.Users;

public sealed record ChangeUserStatusRequest(UserStatus Status);
