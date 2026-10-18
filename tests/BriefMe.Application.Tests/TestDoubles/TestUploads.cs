using BriefMe.Application.UseCases.Briefings;
using BriefMe.Domain.ValueObjects;

namespace BriefMe.Application.Tests.TestDoubles;

/// <summary>Ready-made <see cref="AttachmentUpload"/>s whose bytes start with a real (or deliberately wrong) signature.</summary>
public static class TestUploads
{
    public static AttachmentUpload Pdf(string fileName = "brief.pdf") => From(fileName, [.. "%PDF-1.7\n%âãÏÓ"u8]);

    public static AttachmentUpload Png(string fileName = "logo.png") =>
        From(fileName, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

    public static AttachmentUpload Mp4(string fileName = "reference.mp4") =>
        From(fileName, [0x00, 0x00, 0x00, 0x20, .. "ftyp"u8, .. "isom"u8, 0x00, 0x00, 0x02, 0x00]);

    public static AttachmentUpload Html(string fileName = "evil.png") => From(fileName, [.. "<html><script>"u8]);

    /// <summary>A valid PDF whose declared length is <paramref name="length"/> (the bytes stay small).</summary>
    public static AttachmentUpload PdfDeclaring(long length, string fileName = "big.pdf") =>
        Pdf(fileName) with { Length = length };

    public static AttachmentUpload Empty(string fileName = "empty.pdf") => new(fileName, new MemoryStream(), 0);

    public static AttachmentUpload Oversized(string fileName = "huge.mp4") =>
        Mp4(fileName) with { Length = AttachmentFormat.MaxSizeBytes + 1 };

    private static AttachmentUpload From(string fileName, byte[] bytes) => new(fileName, new MemoryStream(bytes), bytes.Length);
}
