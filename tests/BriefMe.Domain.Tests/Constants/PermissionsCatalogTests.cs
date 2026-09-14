using System.Text.RegularExpressions;
using BriefMe.Domain.Constants;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Constants;

public class PermissionsCatalogTests
{
    // recurso.acao — each segment is one or more lowercase kebab-case tokens.
    private static readonly Regex CodeConvention = new("^[a-z]+(-[a-z]+)*\\.[a-z]+(-[a-z]+)*$");

    [Fact]
    public void Catalog_WhenInspected_HasUniqueCodes()
    {
        // Act
        var codes = Permissions.Catalog.Select(definition => definition.Code).ToList();

        // Assert
        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_WhenInspected_EveryCodeFollowsResourceActionConvention()
    {
        // Act
        var codes = Permissions.Catalog.Select(definition => definition.Code);

        // Assert
        codes.Should().OnlyContain(code => CodeConvention.IsMatch(code));
    }

    [Fact]
    public void Catalog_WhenInspected_EveryDefinitionHasNameAndGroup()
    {
        // Assert
        Permissions.Catalog.Should().OnlyContain(definition =>
            !string.IsNullOrWhiteSpace(definition.Name) && !string.IsNullOrWhiteSpace(definition.Group));
    }

    [Fact]
    public void Catalog_WhenInspected_ContainsAllBacklogCodes()
    {
        // Arrange
        string[] expected =
        [
            "users.view", "users.create", "users.update", "users.delete", "users.manage", "users.reset-password",
            "roles.manage", "permissions.manage",
            "briefings.approve", "briefings.reject", "briefings.request-adjustments", "briefings.view-all",
            "briefings.create", "briefings.view-own",
            "tasks.create", "tasks.assign", "tasks.reassign", "tasks.reopen", "tasks.update-status",
            "tasks.comment", "tasks.deliver", "tasks.approve-technical", "tasks.view-assigned", "tasks.view-all",
            "sla.configure", "categories.configure", "custom-fields.configure", "checklists.configure",
            "audit.view", "system.configure"
        ];

        // Act
        var codes = Permissions.Catalog.Select(definition => definition.Code);

        // Assert
        codes.Should().BeEquivalentTo(expected);
    }
}
