using BriefMe.Domain.Entities;

namespace BriefMe.Application.Abstractions;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
}
