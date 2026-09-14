using BriefMe.Web.Auth;
using FluentAssertions;

namespace BriefMe.Web.Tests.Auth;

public class LoginRedirectUrlBuilderTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void Build_WhenRelativePathIsRootOrEmpty_ReturnsLoginPathWithoutReturnUrl(string relativePath)
    {
        // Arrange
        // (none needed)

        // Act
        var result = LoginRedirectUrlBuilder.Build(relativePath);

        // Assert
        result.Should().Be("/login");
    }

    [Fact]
    public void Build_WhenRelativePathIsNestedSegment_ReturnsLoginPathWithRootedEncodedReturnUrl()
    {
        // Arrange
        const string relativePath = "users/42";

        // Act
        var result = LoginRedirectUrlBuilder.Build(relativePath);

        // Assert
        result.Should().Be("/login?returnUrl=%2Fusers%2F42");
    }

    [Fact]
    public void Build_WhenRelativePathAlreadyRooted_DoesNotDoubleTheLeadingSlash()
    {
        // Arrange
        const string relativePath = "/usuarios";

        // Act
        var result = LoginRedirectUrlBuilder.Build(relativePath);

        // Assert
        result.Should().Be("/login?returnUrl=%2Fusuarios");
    }

    [Fact]
    public void Build_WhenRelativePathHasQueryString_ReturnsRootedReturnUrlThatRoundTripsExactly()
    {
        // Arrange
        const string relativePath = "users?page=2";

        // Act
        var result = LoginRedirectUrlBuilder.Build(relativePath);
        var returnUrlValue = result["/login?returnUrl=".Length..];
        var decoded = Uri.UnescapeDataString(returnUrlValue);

        // Assert
        decoded.Should().Be($"/{relativePath}");
    }

    [Theory]
    [InlineData("usuarios")]
    [InlineData("users/42")]
    [InlineData("users?page=2")]
    public void Build_WhenRelativePathIsUnrooted_ProducesReturnUrlAcceptedByTheResolver(string relativePath)
    {
        // Arrange
        // (none needed)

        // Act
        var built = LoginRedirectUrlBuilder.Build(relativePath);
        var returnUrl = Uri.UnescapeDataString(built["/login?returnUrl=".Length..]);
        var resolved = LoginReturnUrlResolver.Resolve(returnUrl);

        // Assert — the returnUrl survives the Build → Resolve round-trip instead of falling back to "/".
        resolved.Should().Be($"/{relativePath}");
    }
}
