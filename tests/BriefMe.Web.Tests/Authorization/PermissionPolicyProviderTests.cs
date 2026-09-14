using BriefMe.Web.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BriefMe.Web.Tests.Authorization;

public class PermissionPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_WhenPolicyNameIsNotExplicitlyRegistered_ReturnsPolicyWithPermissionRequirement()
    {
        // Arrange
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        // Act
        var policy = await provider.GetPolicyAsync("users.create");

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<PermissionRequirement>()
            .Which.Permissions.Should().Equal("users.create");
    }

    [Fact]
    public async Task GetPolicyAsync_WhenPolicyNameListsSeveralPermissions_ReturnsRequirementWithAllOfThem()
    {
        // Arrange
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        // Act
        var policy = await provider.GetPolicyAsync("tasks.view-assigned,tasks.view-all");

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<PermissionRequirement>()
            .Which.Permissions.Should().Equal("tasks.view-assigned", "tasks.view-all");
    }

    [Fact]
    public async Task GetPolicyAsync_WhenPolicyNameIsExplicitlyRegistered_ReturnsRegisteredPolicy()
    {
        // Arrange
        var registeredPolicy = new AuthorizationPolicyBuilder().RequireRole("Administrador").Build();
        var authorizationOptions = new AuthorizationOptions();
        authorizationOptions.AddPolicy("AdminOnly", registeredPolicy);
        var provider = new PermissionPolicyProvider(Options.Create(authorizationOptions));

        // Act
        var policy = await provider.GetPolicyAsync("AdminOnly");

        // Assert
        policy.Should().BeSameAs(registeredPolicy);
    }
}
