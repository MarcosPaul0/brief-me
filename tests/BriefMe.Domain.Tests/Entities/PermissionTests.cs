using BriefMe.Domain.Entities;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class PermissionTests
{
    [Fact]
    public void Create_WhenCodeIsValid_CreatesPermission()
    {
        // Arrange
        // (none needed)

        // Act
        var permission = Permission.Create("users.create", "Criar usuários", null, "Usuários");

        // Assert
        permission.Code.Should().Be("users.create");
        permission.Group.Should().Be("Usuários");
    }

    [Fact]
    public void Create_WhenValuesHaveSurroundingWhitespace_TrimsCodeNameAndGroup()
    {
        // Arrange
        // (none needed)

        // Act
        var permission = Permission.Create("  users.create  ", "  Criar usuários  ", null, "  Usuários  ");

        // Assert
        permission.Code.Should().Be("users.create");
        permission.Name.Should().Be("Criar usuários");
        permission.Group.Should().Be("Usuários");
    }

    [Fact]
    public void Create_WhenDescriptionProvided_SetsDescription()
    {
        // Arrange
        // (none needed)

        // Act
        var permission = Permission.Create("users.create", "Criar usuários", "Permite criar usuários", "Usuários");

        // Assert
        permission.Description.Should().Be("Permite criar usuários");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenNameIsBlank_ThrowsDomainException(string? name)
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => Permission.Create("users.create", name!, null, "Usuários");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenCodeIsBlank_ThrowsDomainException(string? code)
    {
        // Arrange
        // (none needed)

        // Act
        var act = () => Permission.Create(code!, "Nome", null, "Grupo");

        // Assert
        act.Should().Throw<DomainException>();
    }
}
