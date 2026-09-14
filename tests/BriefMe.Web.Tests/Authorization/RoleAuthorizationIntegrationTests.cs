using System.Security.Claims;
using BriefMe.Domain.Constants;
using BriefMe.Web.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BriefMe.Web.Tests.Authorization;

public class RoleAuthorizationIntegrationTests
{
    private readonly IAuthorizationService _authorizationService;

    public RoleAuthorizationIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        _authorizationService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUser(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "TestAuth"));

    [Fact]
    public async Task AuthorizeAsync_WhenDesignerAccessesAdministradorRestrictedResource_Fails()
    {
        // Arrange
        var user = CreateUser(Roles.Designer);
        var policy = new AuthorizationPolicyBuilder().RequireRole(Roles.Administrador).Build();

        // Act
        var result = await _authorizationService.AuthorizeAsync(user, resource: null, policy);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAdministradorAccessesAdministradorRestrictedResource_Succeeds()
    {
        // Arrange
        var user = CreateUser(Roles.Administrador);
        var policy = new AuthorizationPolicyBuilder().RequireRole(Roles.Administrador).Build();

        // Act
        var result = await _authorizationService.AuthorizeAsync(user, resource: null, policy);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
