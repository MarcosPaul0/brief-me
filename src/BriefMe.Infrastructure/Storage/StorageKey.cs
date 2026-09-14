namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Generates the opaque storage keys returned by <c>IFileStorageService.SaveAsync</c> (BRF-011). Shared by
/// every provider so a key written by one is addressable by another — the format is a storage detail and
/// must never be parsed by callers.
/// </summary>
public static class StorageKey
{
    /// <summary>
    /// Builds <c>yyyy/MM/dd/{guid:N}{ext}</c> in UTC. The date partition keeps any single directory (or
    /// object prefix) from growing without bound; the GUID guarantees uniqueness and keeps the
    /// user-controlled file name out of the write path entirely — only its extension is preserved.
    /// </summary>
    public static string Generate(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        // Forward slashes always: the same key has to work as a filesystem path on any OS and as an S3
        // object name, where '/' is the only separator.
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
    }
}
