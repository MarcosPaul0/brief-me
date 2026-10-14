using System.Security.Claims;
using BriefMe.Web.Auth;
using FluentAssertions;

namespace BriefMe.Web.Tests.Auth;

public class RoleLandingPageTests
{
    [Theory]
    [InlineData("Requester", "/briefings/meus")]
    [InlineData("Designer", "/briefings/meu-trabalho")]
    [InlineData("Admin", "/usuarios")]
    public void For_WhenUserHasRole_ReturnsThatRolesMainPage(string role, string expected)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

        // Act
        var result = RoleLandingPage.For(user);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void For_WhenUserHasNoKnownRole_ReturnsNull()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Ana")], "test"));

        // Act
        var result = RoleLandingPage.For(user);

        // Assert
        result.Should().BeNull();
    }
}
