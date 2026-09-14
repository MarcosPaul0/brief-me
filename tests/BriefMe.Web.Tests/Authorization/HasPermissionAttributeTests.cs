using BriefMe.Web.Authorization;
using FluentAssertions;

namespace BriefMe.Web.Tests.Authorization;

public class HasPermissionAttributeTests
{
    [Fact]
    public void Constructor_WhenGivenPermissionCode_SetsPolicyToPermissionCode()
    {
        // Arrange
        // (none needed)

        // Act
        var attribute = new HasPermissionAttribute("users.create");

        // Assert
        attribute.Policy.Should().Be("users.create");
    }

    [Fact]
    public void Constructor_WhenGivenSeveralPermissionCodes_JoinsThemIntoAnAnyOfPolicy()
    {
        // Arrange
        // (none needed)

        // Act
        var attribute = new HasPermissionAttribute("tasks.view-assigned", "tasks.view-all");

        // Assert
        attribute.Policy.Should().Be("tasks.view-assigned,tasks.view-all");
    }
}
