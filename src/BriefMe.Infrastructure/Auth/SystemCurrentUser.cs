using BriefMe.Application.Abstractions;

namespace BriefMe.Infrastructure.Auth;

public class SystemCurrentUser : ICurrentUser
{
    public string? UserId => null;
}
