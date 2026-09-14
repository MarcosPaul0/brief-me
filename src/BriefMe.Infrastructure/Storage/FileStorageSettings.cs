namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Configuration for file storage and upload validation (BRF-011). Bound from the <c>FileStorage</c>
/// section via the Options pattern, mirroring <see cref="Auth.PasswordHasherSettings"/>.
/// </summary>
public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Which <c>IFileStorageService</c> implementation <c>AddInfrastructure</c> registers. Defaults to
    /// <see cref="FileStorageProvider.Local"/> so the app boots with no external dependency; Development
    /// and deployed environments select <see cref="FileStorageProvider.Minio"/>.
    /// </summary>
    public FileStorageProvider Provider { get; init; } = FileStorageProvider.Local;

    /// <summary>
    /// Root directory for stored files, used by the <see cref="FileStorageProvider.Local"/> provider only.
    /// A relative path resolves against the process working directory.
    /// </summary>
    public string RootPath { get; init; } = "App_Data/uploads";

    /// <summary>Maximum allowed file size in bytes. Default 25 MB.</summary>
    public long MaxSizeBytes { get; init; } = 25L * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions, each with a leading dot, matched case-insensitively. An empty array
    /// disables the extension check (size is still enforced).
    /// </summary>
    public string[] AllowedExtensions { get; init; } =
    [
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".svg",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".zip", ".psd", ".ai", ".txt", ".csv"
    ];
}
