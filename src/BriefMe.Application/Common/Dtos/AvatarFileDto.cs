namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// An avatar image ready to be streamed to the client. The caller owns <c>Content</c> and must dispose it;
/// <c>ContentType</c> is the one detected on upload, never the one the client declared.
/// </summary>
public sealed record AvatarFileDto(Stream Content, string ContentType);
