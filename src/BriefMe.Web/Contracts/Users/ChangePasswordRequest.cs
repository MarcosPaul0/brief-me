namespace BriefMe.Web.Contracts.Users;

public sealed record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
