using BriefMe.Application.Abstractions;
using BriefMe.Domain.Enums;

namespace BriefMe.Infrastructure.Auth;

public class SystemCurrentUser : ICurrentUser
{
    public string? UserId => null;

    public UserRole? Role => null;
}
