using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class RoleTests
{
    [Fact]
    public void Create_WhenNameIsValid_CreatesRoleWithNoPermissions()
    {
        // Arrange
        // (none needed)

        // Act
        var role = Role.Create("Designer", "Perfil de designer");

        // Assert
        role.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenNameHasSurroundingWhitespace_TrimsName()
    {
        // Arrange
        // (none needed)

        // Act
        var role = Role.Create("  Designer  ", null);

        // Assert
        role.Name.Should().Be("Designer");
    }

    [Fact]
    public void Create_WhenIsSystemRoleNotSpecified_DefaultsToFalse()
    {
        // Arrange
        // (none needed)

        // Act
        var role = Role.Create("Designer", null);

        // Assert
        role.IsSystemRole.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenNameIsNullOrWhitespace_ThrowsDomainException(string? name)
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => Role.Create(name!, null);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Rename_WhenNameIsValid_TrimsAndUpdatesName()
    {
        // Arrange
        var role = Role.Create("Designer", null);

        // Act
        role.Rename("  Gestor de Design  ");

        // Assert
        role.Name.Should().Be("Gestor de Design");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_WhenNameIsNullOrWhitespace_ThrowsDomainException(string? name)
    {
        // Arrange
        var role = Role.Create("Designer", null);

        // Act
        var act = () => role.Rename(name!);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RemovePermission_WhenPermissionWasAdded_RemovesAssociation()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var permissionId = Guid.NewGuid();
        role.AddPermission(permissionId);

        // Act
        role.RemovePermission(permissionId);

        // Assert
        role.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void AddPermission_WhenCalledTwiceWithSamePermission_DoesNotDuplicate()
    {
        // Arrange
        var role = Role.Create("Designer", null);
        var permissionId = Guid.NewGuid();

        // Act
        role.AddPermission(permissionId);
        role.AddPermission(permissionId);

        // Assert
        role.RolePermissions.Should().ContainSingle();
    }

    [Fact]
    public void ChangeDescription_WhenCalledWithValue_UpdatesDescription()
    {
        // Arrange
        var role = Role.Create("Designer", "Descrição antiga");

        // Act
        role.ChangeDescription(" Descrição nova ");

        // Assert
        role.Description.Should().Be("Descrição nova");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ChangeDescription_WhenValueIsNullOrWhitespace_SetsDescriptionToNull(string? description)
    {
        // Arrange
        var role = Role.Create("Designer", "Descrição antiga");

        // Act
        role.ChangeDescription(description);

        // Assert
        role.Description.Should().BeNull();
    }

    [Fact]
    public void MarkAsDeleted_WhenRoleIsSystemRole_ThrowsDomainException()
    {
        // Arrange
        var role = Role.Create("Administrador", null, isSystemRole: true);

        // Act
        var act = () => role.MarkAsDeleted("admin@briefme.com");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkAsDeleted_WhenRoleIsNotSystemRole_SetsSoftDeleteFields()
    {
        // Arrange
        var role = Role.Create("Solicitante", null);

        // Act
        role.MarkAsDeleted("admin@briefme.com");

        // Assert
        role.IsDeleted.Should().BeTrue();
        role.DeletedAt.Should().NotBeNull();
        role.DeletedBy.Should().Be("admin@briefme.com");
    }
}
