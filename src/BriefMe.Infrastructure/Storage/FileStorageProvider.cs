namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// The <c>IFileStorageService</c> implementations available to <c>AddInfrastructure</c>, selected by the
/// <c>FileStorage:Provider</c> setting.
/// </summary>
public enum FileStorageProvider
{
    /// <summary>Local filesystem under <c>FileStorage:RootPath</c>. No external dependency.</summary>
    Local = 0,

    /// <summary>MinIO (S3-compatible) object storage configured under <c>FileStorage:Minio</c>.</summary>
    Minio = 1
}
