using BriefMe.Application.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// MinIO (S3-compatible) implementation of <see cref="IFileStorageService"/> (BRF-011). Keeps briefing
/// attachments (RF005) and task deliveries (RF022) outside the app's filesystem, so files survive a redeploy
/// and any instance can serve them. Selected by <c>FileStorage:Provider</c>; the Application layer sees the
/// same contract as <see cref="LocalFileStorageService"/> and never learns which one is running.
/// </summary>
/// <remarks>
/// Registered as a singleton alongside a singleton <c>IMinioClient</c>: the client owns an
/// <c>HttpClient</c> that a shorter lifetime would dispose while an upload is still in flight
/// (minio-dotnet#1369).
/// </remarks>
public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;

    /// <summary>
    /// Guards the one-time bucket check. The flag is read without the semaphore on the hot path, so every
    /// upload after the first pays nothing; the semaphore only serializes the racing first callers.
    /// </summary>
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioFileStorageService(IMinioClient client, IOptions<MinioSettings> settings)
    {
        _client = client;
        _bucket = settings.Value.Bucket;
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await EnsureBucketAsync(cancellationToken);

        var key = StorageKey.Generate(fileName);

        // -1 tells the SDK to stream an object of unknown length; the size limit is the validator's job
        // upstream (BRF-011), not this one's.
        var size = content.CanSeek ? content.Length - content.Position : -1L;

        var args = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            // The uploaded name is user input and is not trusted to describe the bytes; the download
            // endpoint sets the header it wants from the stored metadata.
            .WithContentType("application/octet-stream");

        _ = await _client.PutObjectAsync(args, cancellationToken);

        return key;
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage path is required.", nameof(path));

        // GetObjectAsync pushes the bytes through a callback rather than returning a stream, so they are
        // buffered here. Bounded by FileStorage:MaxSizeBytes, which the validator enforces on upload.
        var buffer = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(path)
            .WithCallbackStream((stream, token) => stream.CopyToAsync(buffer, token));

        try
        {
            _ = await _client.GetObjectAsync(args, cancellationToken);
        }
        catch (ObjectNotFoundException exception)
        {
            await buffer.DisposeAsync();

            // Same contract as the local provider: a key we handed out that no longer resolves is a
            // FileNotFoundException, not a provider-specific type leaking into the callers.
            throw new FileNotFoundException($"No stored file at '{path}'.", path, exception);
        }
        catch (BucketNotFoundException exception)
        {
            await buffer.DisposeAsync();
            throw new FileNotFoundException($"No stored file at '{path}'.", path, exception);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Creates the bucket when it is missing, at most once per process. Done lazily rather than at startup
    /// so a MinIO that is briefly down delays an upload instead of preventing the app from booting.
    /// </summary>
    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
            return;

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
                return;

            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucket), cancellationToken);

            if (!exists)
                await CreateBucketAsync(cancellationToken);

            _bucketReady = true;
        }
        finally
        {
            _ = _bucketLock.Release();
        }
    }

    /// <summary>
    /// Tolerates losing the creation race to another instance starting at the same moment: the outcome we
    /// need is "the bucket exists", so a failure is only fatal when it still does not.
    /// </summary>
    private async Task CreateBucketAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), cancellationToken);
        }
        catch (MinioException)
        {
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucket), cancellationToken);

            if (!exists)
                throw;
        }
    }
}
