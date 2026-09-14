using System.Security.Claims;
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
}
