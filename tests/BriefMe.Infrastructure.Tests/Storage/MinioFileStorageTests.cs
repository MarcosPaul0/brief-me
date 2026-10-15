using BriefMe.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;

namespace BriefMe.Infrastructure.Tests.Storage;

/// <summary>
/// Covers the guards that run before any request leaves the process, so no test contacts a server. The
/// upload/download round trip is exercised manually against the compose stack (user-avatar spec).
/// </summary>
public sealed class MinioFileStorageTests
{
    private readonly MinioFileStorage _storage = new(
        new MinioClient()
            .WithEndpoint("localhost:9000")
            .WithCredentials("test", "test-secret")
            .WithSSL(false)
            .Build(),
        Options.Create(new ObjectStorageSettings { Bucket = "test-bucket" }),
        NullLogger<MinioFileStorage>.Instance);

    [Fact]
    public async Task SaveAsync_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var act = () => _storage.SaveAsync("avatars/a.png", null!, "image/png");

        // Act
        // (the call itself)

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WhenKeyIsBlank_ThrowsArgumentException(string key)
    {
        // Arrange
        var act = () => _storage.SaveAsync(key, new MemoryStream([1]), "image/png");

        // Act
        // (the call itself)

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OpenReadAsync_WhenKeyIsBlank_ThrowsArgumentException(string key)
    {
        // Arrange
        var act = () => _storage.OpenReadAsync(key);

        // Act
        // (the call itself)

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
