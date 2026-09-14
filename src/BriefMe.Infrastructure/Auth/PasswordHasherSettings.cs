namespace BriefMe.Infrastructure.Auth;

public sealed class PasswordHasherSettings
{
    public const string SectionName = "PasswordHasher";

    public int WorkFactor { get; init; } = 11;
}
