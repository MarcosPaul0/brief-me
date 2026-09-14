using BriefMe.Web.Auth;
using FluentAssertions;

namespace BriefMe.Web.Tests.Auth;

public class LoginReturnUrlResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_WhenReturnUrlIsNullOrEmpty_ReturnsRootPath(string? returnUrl)
    {
        // Arrange
        // (none needed)

        // Act
        var result = LoginReturnUrlResolver.Resolve(returnUrl);

        // Assert
        result.Should().Be("/");
    }

    [Theory]
    [InlineData("/users")]
    [InlineData("/users/42")]
    [InlineData("/users?page=2")]
    public void Resolve_WhenReturnUrlIsSameOriginRelativePath_ReturnsItUnchanged(string returnUrl)
    {
        // Arrange
        // (none needed)

        // Act
        var result = LoginReturnUrlResolver.Resolve(returnUrl);

        // Assert
        result.Should().Be(returnUrl);
    }

    [Theory]
    [InlineData("evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("http://evil.com")]
    [InlineData("https://evil.com")]
    [InlineData("javascript:alert(1)")]
    public void Resolve_WhenReturnUrlIsNotASameOriginRelativePath_ReturnsRootPath(string returnUrl)
    {
        // Arrange
        // (none needed)

        // Act
        var result = LoginReturnUrlResolver.Resolve(returnUrl);

        // Assert
        result.Should().Be("/");
    }
}
