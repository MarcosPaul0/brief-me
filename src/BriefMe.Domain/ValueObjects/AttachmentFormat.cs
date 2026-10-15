using BriefMe.Domain.Common;
using BriefMe.Domain.Enums;

namespace BriefMe.Domain.ValueObjects;

/// <summary>
/// The file formats a briefing attachment may have (ATT-03): only formats every evergreen browser previews
/// natively. Recognised by their file signature rather than by the uploaded name or declared content type —
/// both are client input. The content type stored and later served comes from here, so the API never hands
/// back bytes under a type they do not have.
/// </summary>
public sealed class AttachmentFormat : IEquatable<AttachmentFormat>
{
    /// <summary>Upper bound for a single attachment (ATT-04): 25 MB.</summary>
    public const long MaxSizeBytes = 25 * 1024 * 1024;

    /// <summary>How many leading bytes <see cref="Detect"/> needs to tell every format apart.</summary>
    public const int HeaderLength = 16;

    public const string UnsupportedFormatMessage =
        "Unsupported file format. Accepted: PDF, PNG, JPEG, WebP, GIF, MP3, WAV, OGG, M4A, MP4, WebM.";

    public static readonly AttachmentFormat Pdf = new("application/pdf", ".pdf", AttachmentKind.Pdf);

    public static readonly AttachmentFormat Png = new("image/png", ".png", AttachmentKind.Image);

    public static readonly AttachmentFormat Jpeg = new("image/jpeg", ".jpg", AttachmentKind.Image);

    public static readonly AttachmentFormat WebP = new("image/webp", ".webp", AttachmentKind.Image);

    public static readonly AttachmentFormat Gif = new("image/gif", ".gif", AttachmentKind.Image);

    public static readonly AttachmentFormat Mp3 = new("audio/mpeg", ".mp3", AttachmentKind.Audio);

    public static readonly AttachmentFormat Wav = new("audio/wav", ".wav", AttachmentKind.Audio);

    public static readonly AttachmentFormat Ogg = new("audio/ogg", ".ogg", AttachmentKind.Audio);

    public static readonly AttachmentFormat M4A = new("audio/mp4", ".m4a", AttachmentKind.Audio);

    public static readonly AttachmentFormat Mp4 = new("video/mp4", ".mp4", AttachmentKind.Video);

    public static readonly AttachmentFormat WebM = new("video/webm", ".webm", AttachmentKind.Video);

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] Id3Signature = "ID3"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPMarker = "WEBP"u8.ToArray();
    private static readonly byte[] WaveMarker = "WAVE"u8.ToArray();
    private static readonly byte[] OggSignature = "OggS"u8.ToArray();
    private static readonly byte[] FtypMarker = "ftyp"u8.ToArray();
    private static readonly byte[] EbmlSignature = [0x1A, 0x45, 0xDF, 0xA3];

    private static readonly byte[] M4ABrand = "M4A "u8.ToArray();

    // ISO-BMFF major brands written by common encoders for plain MP4 video.
    private static readonly byte[][] Mp4Brands =
    [
        "isom"u8.ToArray(),
        "iso2"u8.ToArray(),
        "mp41"u8.ToArray(),
        "mp42"u8.ToArray(),
        "avc1"u8.ToArray(),
        "dash"u8.ToArray(),
        "MSNV"u8.ToArray()
    ];

    private AttachmentFormat(string contentType, string extension, AttachmentKind kind)
    {
        ContentType = contentType;
        Extension = extension;
        Kind = kind;
    }

    public string ContentType { get; }

    /// <summary>File extension, leading dot included, used when naming the stored object.</summary>
    public string Extension { get; }

    public AttachmentKind Kind { get; }

    /// <summary>Identifies the format from the first bytes of the file (at least <see cref="HeaderLength"/> when available).</summary>
    public static Result<AttachmentFormat> Detect(ReadOnlySpan<byte> header)
    {
        var format = DetectOrNull(header);

        return format is null
            ? Result.Failure<AttachmentFormat>(UnsupportedFormatMessage)
            : Result.Success(format);
    }

    private static AttachmentFormat? DetectOrNull(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature))
            return Pdf;

        if (header.StartsWith(PngSignature))
            return Png;

        if (header.StartsWith(JpegSignature))
            return Jpeg;

        if (header.StartsWith(Gif87Signature) || header.StartsWith(Gif89Signature))
            return Gif;

        // RIFF container: "RIFF" + 4-byte little-endian size + form type.
        if (header.Length >= 12 && header.StartsWith(RiffSignature))
        {
            var formType = header[8..12];
            if (formType.SequenceEqual(WebPMarker))
                return WebP;

            if (formType.SequenceEqual(WaveMarker))
                return Wav;

            return null;
        }

        if (header.StartsWith(OggSignature))
            return Ogg;

        if (header.StartsWith(EbmlSignature))
            return WebM;

        // ISO-BMFF: 4-byte box size + "ftyp" + 4-byte major brand.
        if (header.Length >= 12 && header[4..8].SequenceEqual(FtypMarker))
        {
            var brand = header[8..12];
            if (brand.SequenceEqual(M4ABrand))
                return M4A;

            foreach (var mp4Brand in Mp4Brands)
            {
                if (brand.SequenceEqual(mp4Brand))
                    return Mp4;
            }

            return null;
        }

        if (header.StartsWith(Id3Signature) || IsMpegAudioFrameSync(header))
            return Mp3;

        return null;
    }

    /// <summary>
    /// A raw MP3 without an ID3 tag starts with an MPEG audio frame header: 11 set sync bits, then a version
    /// other than "reserved" and layer III (FB / FA / F3 / F2 / E3 / E2 as the second byte).
    /// </summary>
    private static bool IsMpegAudioFrameSync(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2 || header[0] != 0xFF)
            return false;

        var second = header[1];
        var hasSync = (second & 0xE0) == 0xE0;
        var version = (second >> 3) & 0b11;
        var layer = (second >> 1) & 0b11;

        return hasSync && version != 0b01 && layer == 0b01;
    }

    public bool Equals(AttachmentFormat? other) => other is not null && ContentType == other.ContentType;

    public override bool Equals(object? obj) => Equals(obj as AttachmentFormat);

    public override int GetHashCode() => ContentType.GetHashCode();

    public override string ToString() => ContentType;
}
