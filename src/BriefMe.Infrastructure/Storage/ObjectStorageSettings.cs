namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Connection to the S3-compatible object store behind <see cref="MinioFileStorage"/> — MinIO from
/// <c>docker-compose.yml</c> locally; any S3 endpoint works since the SDK speaks plain S3.
/// </summary>
public sealed class ObjectStorageSettings
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Host and port of the S3 API, without a scheme (e.g. <c>localhost:9000</c>).</summary>
    public string Endpoint { get; init; } = "localhost:9000";

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Bucket every object is stored in. Created on first write when missing.</summary>
    public string Bucket { get; init; } = "briefme";

    /// <summary>HTTPS to the storage endpoint. False for the local compose stack.</summary>
    public bool UseSsl { get; init; }
}
