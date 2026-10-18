namespace BriefMe.Application.Abstractions;

/// <summary>
/// Issues and checks the short-lived tokens that let a media element (<c>&lt;img&gt;</c>, <c>&lt;video&gt;</c>, ...)
/// load an attachment without a bearer token (ATT-18..20). A token is only issued after the caller passed the
/// briefing's read check, so holding a valid one is the authorization for that single attachment.
/// </summary>
public interface IAttachmentLinkSigner
{
    /// <summary>An opaque, URL-safe token bound to this briefing and attachment; its expiry travels inside it.</summary>
    string Issue(Guid briefingId, Guid attachmentId);

    /// <summary>
    /// True only for an unexpired token issued for exactly these ids. Missing, malformed, tampered and expired
    /// tokens are all just <see langword="false"/> — callers answer them the same way.
    /// </summary>
    bool Verify(Guid briefingId, Guid attachmentId, string? token);
}
