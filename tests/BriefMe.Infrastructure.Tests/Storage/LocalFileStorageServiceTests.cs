using System.Text;
using BriefMe.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BriefMe.Infrastructure.Tests.Storage;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorageService _storage;

    public LocalFileStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"briefme-storage-tests-{Guid.NewGuid():N}");
        _storage = new LocalFileStorageService(
            Options.Create(new FileStorageSettings { RootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream StreamOf(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task SaveAsync_WhenCalled_ReturnsNonEmptyKeyPreservingExtension()
    {
        // Arrange
        using var content = StreamOf("hello");

        // Act
        var key = await _storage.SaveAsync(content, "brief.pdf");

        // Assert
        key.Should().NotBeNullOrWhiteSpace();
        key.Should().EndWith(".pdf");
        key.Should().NotContain("\\");
    }

    [Fact]
    public async Task GetAsync_WhenKeyFromSaveAsync_ReturnsOriginalBytes()
    {
        // Arrange
        const string original = "the original content";
        using var content = StreamOf(original);
        var key = await _storage.SaveAsync(content, "brief.txt");

        // Act
        await using var retrieved = await _storage.GetAsync(key);
        using var reader = new StreamReader(retrieved);
        var actual = await reader.ReadToEndAsync();

        // Assert
        actual.Should().Be(original);
    }

    [Fact]
    public async Task SaveAsync_WhenSameFileNameSavedTwice_ReturnsDistinctKeys()
    {
        // Arrange
        using var first = StreamOf("a");
        using var second = StreamOf("b");

        // Act
        var keyA = await _storage.SaveAsync(first, "same.png");
        var keyB = await _storage.SaveAsync(second, "same.png");

        // Assert
        keyA.Should().NotBe(keyB);
    }

    [Fact]
    public async Task SaveAsync_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => _storage.SaveAsync(null!, "brief.pdf");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAsync_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}.pdf";

        // Act
        var act = () => _storage.GetAsync(missingKey);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../outside.txt")]
    public async Task GetAsync_WhenKeyEscapesRoot_ThrowsUnauthorizedAccessException(string key)
    {
        // Arrange
        // (none)

        // Act
        var act = () => _storage.GetAsync(key);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WhenKeyIsBlank_ThrowsArgumentException(string key)
    {
        // Arrange
        // (none)

        // Act
        var act = () => _storage.GetAsync(key);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
