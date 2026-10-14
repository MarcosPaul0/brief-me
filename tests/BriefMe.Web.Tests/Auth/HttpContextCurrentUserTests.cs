using System.Security.Claims;
using BriefMe.Domain.Enums;
using BriefMe.Web.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace BriefMe.Web.Tests.Auth;

public class HttpContextCurrentUserTests
{
    [Fact]
    public void UserId_WhenHttpContextUserHasNameIdentifierClaim_ReturnsClaimValue()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "TestAuth");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        var currentUser = new HttpContextCurrentUser(accessor);

        // Act
        var result = currentUser.UserId;

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void UserId_WhenHttpContextUserHasNoNameIdentifierClaim_ReturnsNull()
    {
        // Arrange
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var currentUser = new HttpContextCurrentUser(accessor);

        // Act
        var result = currentUser.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenHttpContextIsNull_ReturnsNull()
    {
        // Arrange
        var accessor = new HttpContextAccessor();
        var currentUser = new HttpContextCurrentUser(accessor);

        // Act
        var result = currentUser.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Designer", UserRole.Designer)]
    [InlineData("Requester", UserRole.Requester)]
    public void Role_WhenRoleClaimIsKnown_ReturnsTheRole(string claim, UserRole expected)
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, claim)], authenticationType: "TestAuth");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        var currentUser = new HttpContextCurrentUser(accessor);

        // Act
        var result = currentUser.Role;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Role_WhenRoleClaimIsUnknown_ReturnsNull()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Gestor de Design")], authenticationType: "TestAuth");
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        var currentUser = new HttpContextCurrentUser(accessor);

        // Act
        var result = currentUser.Role;

        // Assert
        result.Should().BeNull();
    }
}
