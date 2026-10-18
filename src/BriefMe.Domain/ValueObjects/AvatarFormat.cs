using BriefMe.Domain.Common;

namespace BriefMe.Domain.ValueObjects;

/// <summary>
/// The image formats a user avatar may have (AVT-03), recognised by their file signature rather than by the
/// uploaded name or declared content type — both are client input. The content type stored and later served
/// comes from here, so the API never hands back bytes under a type they do not have.
/// </summary>
public sealed class AvatarFormat : IEquatable<AvatarFormat>
{
    /// <summary>Upper bound for an avatar upload (AVT-04): 2 MB.</summary>
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    public const string InvalidFormatMessage = "Formato de imagem inválido. Use PNG, JPEG ou WebP.";

    public static readonly AvatarFormat Png = new("image/png", ".png");

    public static readonly AvatarFormat Jpeg = new("image/jpeg", ".jpg");

    public static readonly AvatarFormat WebP = new("image/webp", ".webp");

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPMarker = "WEBP"u8.ToArray();

    private AvatarFormat(string contentType, string extension)
    {
        ContentType = contentType;
        Extension = extension;
    }

    public string ContentType { get; }

    /// <summary>File extension, leading dot included, used when naming the stored object.</summary>
    public string Extension { get; }

    /// <summary>Identifies the format from the first bytes of the file.</summary>
    public static Result<AvatarFormat> Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PngSignature))
            return Result.Success(Png);

        if (header.StartsWith(JpegSignature))
            return Result.Success(Jpeg);

        // RIFF container: "RIFF" + 4-byte little-endian size + "WEBP".
        if (header.Length >= 12 && header.StartsWith(RiffSignature) && header[8..12].SequenceEqual(WebPMarker))
            return Result.Success(WebP);

        return Result.Failure<AvatarFormat>(InvalidFormatMessage);
    }

    public bool Equals(AvatarFormat? other) => other is not null && ContentType == other.ContentType;

    public override bool Equals(object? obj) => Equals(obj as AvatarFormat);

    public override int GetHashCode() => ContentType.GetHashCode();

    public override string ToString() => ContentType;
}
