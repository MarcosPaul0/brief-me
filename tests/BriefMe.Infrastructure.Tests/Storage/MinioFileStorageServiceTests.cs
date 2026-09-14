using BriefMe.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Minio;

namespace BriefMe.Infrastructure.Tests.Storage;

/// <summary>
/// Covers the guards that run before any request leaves the process, so no test contacts a server. The
/// upload/download round-trip is exercised manually against the compose stack (see the feature spec).
/// </summary>
public sealed class MinioFileStorageServiceTests
{
    private readonly MinioFileStorageService _storage = new(
        new MinioClient()
            .WithEndpoint("localhost:9000")
            .WithCredentials("test", "test-secret")
            .WithSSL(false)
            .Build(),
        Options.Create(new MinioSettings { Bucket = "test-bucket" }));

    [Fact]
    public async Task SaveAsync_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var act = () => _storage.SaveAsync(null!, "brief.pdf");

        // Act
        // (the call itself)

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WhenPathIsBlank_ThrowsArgumentException(string path)
    {
        // Arrange
        var act = () => _storage.GetAsync(path);

        // Act
        // (the call itself)

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
