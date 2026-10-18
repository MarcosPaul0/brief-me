namespace BriefMe.Application.UseCases.Briefings;

/// <summary>
/// A file sent with a create or edit command. <paramref name="Length"/> is the received size, checked before
/// any byte is read. <paramref name="Content"/> must be readable and seekable (form-file streams are): the
/// format is detected from its first bytes before anything is stored, then the stream is rewound and saved.
/// The caller owns and disposes the stream.
/// </summary>
public sealed record AttachmentUpload(string? FileName, Stream Content, long Length);
