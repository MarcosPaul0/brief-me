using System.Reflection;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class AttachmentTests
{
    private static Attachment NewAttachment() =>
        Attachment.Create(Guid.NewGuid(), "brief.pdf", "/storage/brief.pdf", 1024, Guid.NewGuid());

    [Fact]
    public void Create_WhenDataIsValid_SetsMetadataAndStampsUploadedAt()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Act
        var attachment = Attachment.Create(briefingId, "  brief.pdf  ", "  /storage/brief.pdf  ", 2048, authorId);

        // Assert
        attachment.BriefingId.Should().Be(briefingId);
        attachment.FileName.Should().Be("brief.pdf");
        attachment.StoragePath.Should().Be("/storage/brief.pdf");
        attachment.SizeBytes.Should().Be(2048);
        attachment.UploadedById.Should().Be(authorId);
        attachment.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WhenAttachedToABriefing_LeavesTaskIdNull()
    {
        // Arrange
        // (none)

        // Act
        var attachment = NewAttachment();

        // Assert
        attachment.TaskId.Should().BeNull();
    }

    [Fact]
    public void CreateDelivery_WhenDataIsValid_LinksBothTaskAndBriefing()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        // Act
        var delivery = Attachment.CreateDelivery(
            briefingId,
            taskId,
            "key-visual.png",
            "/storage/key-visual.png",
            4096,
            authorId);

        // Assert
        delivery.BriefingId.Should().Be(briefingId);
        delivery.TaskId.Should().Be(taskId);
        delivery.UploadedById.Should().Be(authorId);
        delivery.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateDelivery_WhenTaskIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Attachment.CreateDelivery(
            Guid.NewGuid(),
            Guid.Empty,
            "key-visual.png",
            "/storage/key-visual.png",
            4096,
            Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateDelivery_WhenSizeBytesIsNotPositive_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Attachment.CreateDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "key-visual.png",
            "/storage/key-visual.png",
            0,
            Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenFileNameIsNullOrWhitespace_ThrowsDomainException(string? fileName)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Attachment.Create(Guid.NewGuid(), fileName!, "/storage/f", 1024, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenFileNameExceedsTheColumnWidth_ThrowsDomainException()
    {
        // Arrange
        var fileName = new string('a', Attachment.MaxFileNameLength + 1);

        // Act
        var act = () => Attachment.Create(Guid.NewGuid(), fileName, "/storage/f", 1024, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenFileNameIsExactlyTheColumnWidth_Succeeds()
    {
        // Arrange
        var fileName = new string('a', Attachment.MaxFileNameLength);

        // Act
        var attachment = Attachment.Create(Guid.NewGuid(), fileName, "/storage/f", 1024, Guid.NewGuid());

        // Assert
        attachment.FileName.Should().Be(fileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenStoragePathIsNullOrWhitespace_ThrowsDomainException(string? storagePath)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Attachment.Create(Guid.NewGuid(), "f.pdf", storagePath!, 1024, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenSizeBytesIsNotPositive_ThrowsDomainException(long size)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Attachment.Create(Guid.NewGuid(), "f.pdf", "/storage/f", size, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void StoragePath_HasNoPublicSetter()
    {
        // Arrange
        var property = typeof(Attachment).GetProperty(
            nameof(Attachment.StoragePath),
            BindingFlags.Public | BindingFlags.Instance);

        // Act
        var publicSetter = property!.GetSetMethod(nonPublic: false);

        // Assert
        publicSetter.Should().BeNull();
    }

    [Fact]
    public void AllProperties_HaveNoPublicSetter()
    {
        // Arrange
        var properties = typeof(Attachment).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act
        var withPublicSetter = properties
            .Where(p => p.GetSetMethod(nonPublic: false) is not null)
            .Select(p => p.Name)
            .ToArray();

        // Assert
        withPublicSetter.Should().BeEmpty();
    }
}
