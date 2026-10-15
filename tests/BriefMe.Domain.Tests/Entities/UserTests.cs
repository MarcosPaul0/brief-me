using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class UserTests
{
    private static Email ValidEmail() => Email.Create("user@empresa.com").Value;

    [Fact]
    public void Create_WhenDataIsValid_CreatesActiveUser()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;

        // Act
        var user = User.Create("Maria Silva", email, "hashed-password", UserRole.Requester);

        // Assert
        user.Status.Should().Be(UserStatus.Active);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Designer)]
    [InlineData(UserRole.Requester)]
    public void Create_WhenRoleIsValid_KeepsTheRole(UserRole role)
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var user = User.Create("Maria Silva", email, "hashed-password", role);

        // Assert
        user.Role.Should().Be(role);
    }

    [Fact]
    public void Create_WhenRoleIsUndefined_ThrowsDomainException()
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var act = () => User.Create("Maria Silva", email, "hashed-password", (UserRole)99);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeRole_WhenRoleIsValid_ReplacesTheRole()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        user.ChangeRole(UserRole.Designer);

        // Assert
        user.Role.Should().Be(UserRole.Designer);
    }

    [Fact]
    public void ChangeRole_WhenRoleIsUndefined_ThrowsDomainException()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        var act = () => user.ChangeRole((UserRole)99);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenFullNameHasSurroundingWhitespace_TrimsFullName()
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var user = User.Create("  Maria Silva  ", email, "hashed-password", UserRole.Requester);

        // Assert
        user.FullName.Should().Be("Maria Silva");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenFullNameIsNullOrWhitespace_ThrowsDomainException(string? fullName)
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var act = () => User.Create(fullName!, email, "hashed-password", UserRole.Requester);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenPasswordHashIsNullOrWhitespace_ThrowsDomainException(string? passwordHash)
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var act = () => User.Create("Maria Silva", email, passwordHash!, UserRole.Requester);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Deactivate_WhenUserIsActive_SetsStatusToInactive()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;
        var user = User.Create("Maria Silva", email, "hashed-password", UserRole.Requester);

        // Act
        user.Deactivate();

        // Assert
        user.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public void ChangePassword_WhenNewHashProvided_UpdatesPasswordHash()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;
        var user = User.Create("Maria Silva", email, "old-hash", UserRole.Requester);

        // Act
        user.ChangePassword("new-hash");

        // Assert
        user.PasswordHash.Should().Be("new-hash");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangePassword_WhenNewHashIsNullOrWhitespace_ThrowsDomainException(string? newHash)
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "old-hash", UserRole.Requester);

        // Act
        var act = () => user.ChangePassword(newHash!);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateProfile_WhenDataIsValid_UpdatesNameAndEmail()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);
        var newEmail = Email.Create("nova@empresa.com").Value;

        // Act
        user.UpdateProfile("  João Souza  ", newEmail);

        // Assert
        user.FullName.Should().Be("João Souza");
        user.Email.Should().Be(newEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateProfile_WhenFullNameIsNullOrWhitespace_ThrowsDomainException(string? fullName)
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        var act = () => user.UpdateProfile(fullName!, ValidEmail());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Activate_WhenUserIsInactive_SetsStatusToActive()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);
        user.Deactivate();

        // Act
        user.Activate();

        // Assert
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void MarkAsDeleted_WhenCalled_SetsSoftDeleteFields()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        user.MarkAsDeleted("admin@briefme.com");

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.DeletedBy.Should().Be("admin@briefme.com");
    }

    [Fact]
    public void ChangeAvatar_WhenKeyIsValid_StoresKeyContentTypeAndVersion()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        user.ChangeAvatar("avatars/a/b.png", AvatarFormat.Png);

        // Assert
        new { user.AvatarKey, user.AvatarContentType, HasVersion = user.AvatarVersion.HasValue }
            .Should().BeEquivalentTo(new { AvatarKey = "avatars/a/b.png", AvatarContentType = "image/png", HasVersion = true });
    }

    [Fact]
    public void ChangeAvatar_WhenCalledAgain_ProducesANewVersion()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);
        user.ChangeAvatar("avatars/a/first.png", AvatarFormat.Png);
        var firstVersion = user.AvatarVersion;

        // Act
        user.ChangeAvatar("avatars/a/second.jpg", AvatarFormat.Jpeg);

        // Assert
        user.AvatarVersion.Should().NotBe(firstVersion!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeAvatar_WhenKeyIsBlank_ThrowsDomainException(string key)
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);

        // Act
        var act = () => user.ChangeAvatar(key, AvatarFormat.Png);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RemoveAvatar_WhenUserHasAvatar_ClearsAllAvatarFields()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password", UserRole.Requester);
        user.ChangeAvatar("avatars/a/b.png", AvatarFormat.Png);

        // Act
        user.RemoveAvatar();

        // Assert
        new { user.AvatarKey, user.AvatarContentType, user.AvatarVersion }
            .Should().BeEquivalentTo(new { AvatarKey = (string?)null, AvatarContentType = (string?)null, AvatarVersion = (Guid?)null });
    }
}
