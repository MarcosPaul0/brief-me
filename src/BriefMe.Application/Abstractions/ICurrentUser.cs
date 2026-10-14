using BriefMe.Domain.Enums;

namespace BriefMe.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }

    /// <summary>The caller's fixed role, or null when there is no authenticated caller (or an unknown claim).</summary>
    UserRole? Role { get; }
}
