namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// An attachment's bytes ready to be streamed. The caller owns <c>Content</c> and must dispose it;
/// <c>ContentType</c> is the one detected on upload, never the one the client declared.
/// </summary>
public sealed record AttachmentFileDto(Stream Content, string ContentType, string FileName);
