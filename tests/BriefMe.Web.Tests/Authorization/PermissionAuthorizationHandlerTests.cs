using System.Security.Claims;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace BriefMe.Web.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _handler = new();

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

    private static AuthorizationHandlerContext CreateContext(PermissionRequirement requirement, ClaimsPrincipal user) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasMatchingPermissionClaim_Succeeds()
    {
        // Arrange
        var requirement = new PermissionRequirement("users.create");
        var user = CreatePrincipal(new Claim(CustomClaimTypes.Permission, "users.create"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserLacksMatchingPermissionClaim_DoesNotSucceed()
    {
        // Arrange
        var requirement = new PermissionRequirement("users.create");
        var user = CreatePrincipal(new Claim(CustomClaimTypes.Permission, "users.view"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenRequirementListsSeveralPermissionsAndUserHasOne_Succeeds()
    {
        // Arrange — the Design Manager holds tasks.view-all only; either code opens the task detail (BRF-034).
        var requirement = new PermissionRequirement("tasks.view-assigned", "tasks.view-all");
        var user = CreatePrincipal(new Claim(CustomClaimTypes.Permission, "tasks.view-all"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenRequirementListsSeveralPermissionsAndUserHasNone_DoesNotSucceed()
    {
        // Arrange
        var requirement = new PermissionRequirement("tasks.view-assigned", "tasks.view-all");
        var user = CreatePrincipal(new Claim(CustomClaimTypes.Permission, "tasks.comment"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasNoClaims_DoesNotSucceed()
    {
        // Arrange
        var requirement = new PermissionRequirement("users.create");
        var user = CreatePrincipal();
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserIsInAdministradorRole_SucceedsWithoutMatchingClaim()
    {
        // Arrange
        var requirement = new PermissionRequirement("users.create");
        var user = CreatePrincipal(new Claim(ClaimTypes.Role, "Administrador"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasMultiplePermissionClaims_SucceedsOnAnyMatch()
    {
        // Arrange
        var requirement = new PermissionRequirement("users.delete");
        var user = CreatePrincipal(
            new Claim(CustomClaimTypes.Permission, "users.create"),
            new Claim(CustomClaimTypes.Permission, "users.delete"));
        var context = CreateContext(requirement, user);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }
}
