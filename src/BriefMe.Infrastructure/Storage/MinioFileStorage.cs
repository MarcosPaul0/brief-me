using BriefMe.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace BriefMe.Infrastructure.Storage;

/// <summary>
/// MinIO (S3-compatible) implementation of <see cref="IFileStorage"/>. Registered as a singleton alongside a
/// singleton <c>IMinioClient</c>: the client owns an <c>HttpClient</c> that a shorter lifetime would dispose
/// while a request is still in flight (minio-dotnet#1369).
/// </summary>
public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly ILogger<MinioFileStorage> _logger;

    /// <summary>
    /// Guards the one-time bucket check. The flag is read without the semaphore on the hot path, so every
    /// write after the first pays nothing; the semaphore only serializes the racing first callers.
    /// </summary>
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioFileStorage(IMinioClient client, IOptions<ObjectStorageSettings> settings, ILogger<MinioFileStorage> logger)
    {
        _client = client;
        _bucket = settings.Value.Bucket;
        _logger = logger;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        await EnsureBucketAsync(cancellationToken);

        // -1 tells the SDK to stream an object of unknown length; size limits are the caller's job.
        var size = content.CanSeek ? content.Length - content.Position : -1L;

        var args = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType);

        _ = await _client.PutObjectAsync(args, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // GetObjectAsync pushes the bytes through a callback rather than returning a stream, so they are
        // buffered here. Bounded by what callers accept on upload (2 MB for an avatar).
        var buffer = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(key)
            .WithCallbackStream((stream, token) => stream.CopyToAsync(buffer, token));

        try
        {
            _ = await _client.GetObjectAsync(args, cancellationToken);
        }
        catch (Exception exception) when (exception is ObjectNotFoundException or BucketNotFoundException)
        {
            await buffer.DisposeAsync();
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Contract: never fails the change that made the object obsolete; an orphan is the lesser evil.
            _logger.LogWarning(exception, "Could not delete object {Key} from bucket {Bucket}; leaving it orphaned.", key, _bucket);
        }
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

            var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), cancellationToken);
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
            var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), cancellationToken);
            if (!exists)
                throw;
        }
    }
}
