namespace BriefMe.Application.Abstractions;

/// <summary>
/// Abstracts binary file persistence for briefing attachments (RF005), task deliveries (RF022) and
/// versioned files (RF049–RF052). BRF-011. The concrete implementation lives in Infrastructure so the
/// inner layers never bind to a specific provider (local filesystem in dev, blob storage in production).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Persists <paramref name="content"/> under a newly generated, opaque storage key and returns that
    /// key. Callers MUST store the returned key to retrieve the file later — its internal shape is a
    /// storage detail and must not be parsed.
    /// </summary>
    /// <param name="content">The file bytes. Never <see langword="null"/>.</param>
    /// <param name="fileName">Original file name; only its extension is preserved in the key.</param>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the file previously persisted at <paramref name="path"/> for reading. Throws
    /// <see cref="FileNotFoundException"/> when no file exists at that key.
    /// </summary>
    Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default);
}
