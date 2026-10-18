namespace BriefMe.Application.Abstractions;

/// <summary>
/// Binary object storage (S3-compatible in every environment — MinIO under docker compose). The caller picks
/// the key, so the object name can carry the owning entity; the implementation never interprets it.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes <paramref name="content"/> under <paramref name="key"/>, replacing any object already there.</summary>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens the object for reading, or returns <see langword="null"/> when no object exists at <paramref name="key"/>.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Best-effort removal used to drop an object that is no longer referenced. Never throws: a missing object
    /// is already the desired outcome, and a storage fault is logged and leaves an orphan rather than failing
    /// the change that made the object obsolete.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
