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
    public void Create_WhenDataIsValid_CreatesActiveUserWithNoRoles()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;

        // Act
        var user = User.Create("Maria Silva", email, "hashed-password");

        // Assert
        user.Status.Should().Be(UserStatus.Active);
        user.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenFullNameHasSurroundingWhitespace_TrimsFullName()
    {
        // Arrange
        var email = ValidEmail();

        // Act
        var user = User.Create("  Maria Silva  ", email, "hashed-password");

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
        var act = () => User.Create(fullName!, email, "hashed-password");

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
        var act = () => User.Create("Maria Silva", email, passwordHash!);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignRole_WhenCalledTwiceWithSameRole_DoesNotDuplicate()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;
        var user = User.Create("Maria Silva", email, "hashed-password");
        var roleId = Guid.NewGuid();

        // Act
        user.AssignRole(roleId);
        user.AssignRole(roleId);

        // Assert
        user.UserRoles.Should().ContainSingle();
    }

    [Fact]
    public void Deactivate_WhenUserIsActive_SetsStatusToInactive()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;
        var user = User.Create("Maria Silva", email, "hashed-password");

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
        var user = User.Create("Maria Silva", email, "old-hash");

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
        var user = User.Create("Maria Silva", ValidEmail(), "old-hash");

        // Act
        var act = () => user.ChangePassword(newHash!);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateProfile_WhenDataIsValid_UpdatesNameAndEmail()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password");
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
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password");

        // Act
        var act = () => user.UpdateProfile(fullName!, ValidEmail());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Activate_WhenUserIsInactive_SetsStatusToActive()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password");
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
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password");

        // Act
        user.MarkAsDeleted("admin@briefme.com");

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.DeletedBy.Should().Be("admin@briefme.com");
    }

    [Fact]
    public void RemoveRole_WhenRoleWasNotAssigned_DoesNothing()
    {
        // Arrange
        var user = User.Create("Maria Silva", ValidEmail(), "hashed-password");

        // Act
        user.RemoveRole(Guid.NewGuid());

        // Assert
        user.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRole_WhenRoleWasAssigned_RemovesAssociation()
    {
        // Arrange
        var email = Email.Create("user@empresa.com").Value;
        var user = User.Create("Maria Silva", email, "hashed-password");
        var roleId = Guid.NewGuid();
        user.AssignRole(roleId);

        // Act
        user.RemoveRole(roleId);

        // Assert
        user.UserRoles.Should().BeEmpty();
    }
}
