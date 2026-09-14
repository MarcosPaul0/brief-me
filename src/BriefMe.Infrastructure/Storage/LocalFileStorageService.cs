using BriefMe.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// Local-filesystem implementation of <see cref="IFileStorageService"/> (BRF-011), suitable for
/// development. The <see cref="IFileStorageService"/> seam lets production swap in blob storage without
/// touching the Application layer.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<FileStorageSettings> settings)
    {
        _rootPath = Path.GetFullPath(settings.Value.RootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Key policy lives in StorageKey so this provider and the MinIO one cannot drift apart.
        var relativePath = StorageKey.Generate(fileName);

        var absolutePath = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var target = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);

        // Already forward-slashed by StorageKey, so the stored key is portable across operating systems.
        return relativePath;
    }

    public Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWithinRoot(path);

        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"No stored file at '{path}'.", path);

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <summary>
    /// Resolves <paramref name="path"/> against the storage root and guarantees the result stays inside
    /// it, defeating traversal keys such as <c>../../etc/passwd</c>.
    /// </summary>
    private string ResolveWithinRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage path is required.", nameof(path));

        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, path));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!absolutePath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Path '{path}' is outside the storage root.");

        return absolutePath;
    }
}
