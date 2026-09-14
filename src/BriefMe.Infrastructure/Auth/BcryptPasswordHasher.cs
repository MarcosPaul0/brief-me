using BriefMe.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Auth;

public sealed class BcryptPasswordHasher(IOptions<PasswordHasherSettings> settings) : IPasswordHasher
{
    private readonly int _workFactor = settings.Value.WorkFactor;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
