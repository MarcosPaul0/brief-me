namespace BriefMe.Infrastructure.Auth;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    public int ExpirationDays { get; init; } = 7;
}
