namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Connection details for the MinIO bucket that backs <see cref="MinioFileStorageService"/>. Bound from the
/// nested <c>FileStorage:Minio</c> section, keeping provider-specific fields out of
/// <see cref="FileStorageSettings"/>, which only decides <i>which</i> provider runs.
/// </summary>
public sealed class MinioSettings
{
    public const string SectionName = $"{FileStorageSettings.SectionName}:Minio";

    /// <summary>Host and port of the S3 API, without a scheme (e.g. <c>localhost:9000</c>).</summary>
    public string Endpoint { get; init; } = "localhost:9000";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Bucket every attachment and delivery is stored in. Created on first use when missing.</summary>
    public string Bucket { get; init; } = "briefme-attachments";

    /// <summary>HTTPS to the storage endpoint. False for the local compose stack.</summary>
    public bool UseSsl { get; init; }
}
