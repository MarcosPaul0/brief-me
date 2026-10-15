namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Signing of the attachment content links (<see cref="HmacAttachmentLinkSigner"/>). A key of its own rather
/// than the JWT secret, so rotating one never invalidates the other.
/// </summary>
public sealed class AttachmentLinkSettings
{
    public const string SectionName = "AttachmentLinks";

    /// <summary>Minimum key length in bytes (UTF-8): HMAC-SHA256 gains nothing from a key shorter than its output.</summary>
    public const int MinimumKeyBytes = 32;

    public string SigningKey { get; init; } = string.Empty;

    /// <summary>How long an issued link stays valid (ATT-20).</summary>
    public int LifetimeMinutes { get; init; } = 15;
}
