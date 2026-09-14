using BriefMe.Domain.Constants;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Common;

public class RolesTests
{
    [Fact]
    public void All_WhenInspected_ContainsTheFourNativeRolesWithoutDuplicates()
    {
        // Arrange
        // (none needed)

        // Act
        var roles = Roles.All;

        // Assert
        roles.Should().BeEquivalentTo(
        [
            Roles.Administrador,
            Roles.GestorDeDesign,
            Roles.Designer,
            Roles.Solicitante
        ]);
        roles.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_WhenInspected_CoversTheFourNativeRoles()
    {
        // Arrange
        // (none needed)

        // Act
        var names = Roles.Catalog.Select(definition => definition.Name);

        // Assert
        names.Should().BeEquivalentTo(Roles.All);
    }

    [Fact]
    public void Catalog_WhenInspected_OnlyReferencesExistingCatalogCodes()
    {
        // Arrange
        var catalogCodes = Permissions.Catalog.Select(definition => definition.Code).ToHashSet();

        // Act
        var referencedCodes = Roles.Catalog.SelectMany(definition => definition.PermissionCodes).Distinct();

        // Assert
        referencedCodes.Should().OnlyContain(code => catalogCodes.Contains(code));
    }

    [Fact]
    public void Catalog_ForAdministrador_GrantsEveryPermissionInTheCatalog()
    {
        // Arrange
        var catalogCodes = Permissions.Catalog.Select(definition => definition.Code);

        // Act
        var administrador = Roles.Catalog.Single(definition => definition.Name == Roles.Administrador);

        // Assert
        administrador.PermissionCodes.Should().BeEquivalentTo(catalogCodes);
    }
}
