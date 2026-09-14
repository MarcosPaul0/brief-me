using BriefMe.Application.Abstractions;
using BriefMe.Application.Abstractions.Messaging;
using BriefMe.Application.Common.Dtos;
using BriefMe.Application.UseCases.BriefingTasks.AssignTask;
using BriefMe.Application.UseCases.BriefingTasks.CreateBriefingTasks;
using BriefMe.Application.UseCases.BriefingTasks.GetTaskDetail;
using BriefMe.Application.UseCases.BriefingTasks.ReassignTask;
using BriefMe.Application.UseCases.BriefingTasks.RegisterComment;
using BriefMe.Application.UseCases.BriefingTasks.ReopenTask;
using BriefMe.Application.UseCases.BriefingTasks.SetDeadlinePriority;
using BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;
using BriefMe.Application.UseCases.BriefingTasks.UpdateTaskStatus;
using BriefMe.Application.UseCases.Sla.GetOverdueTasks;
using BriefMe.Domain.Common;
using BriefMe.Domain.Constants;
using BriefMe.Domain.Enums;
using BriefMe.Infrastructure.Auth;
using BriefMe.Web.Auth;
using BriefMe.Web.Contracts.Tasks;
using BriefMe.Web.Controllers;
using BriefMe.Web.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace BriefMe.Web.Tests.Controllers;

public class TasksControllerTests
{
    private const string SecretKey = "web-tests-tasks-controller-secret-key-32bytes!";

    private static JwtSettings CreateJwtSettings() => new()
    {
        Issuer = "BriefMe.Web.Tests",
        Audience = "BriefMe.Web.Tests.Audience",
        SecretKey = SecretKey,
        AccessTokenMinutes = 15
    };

    // Mirrors BriefingsControllerTests: builds a token straight from claims, since these tests target the
    // Web-layer authorization pipeline (permission policies), not JwtService's claim population.
    private static string GenerateToken(JwtSettings settings, Guid userId, string[]? roles = null, string[]? permissions = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((permissions ?? []).Select(permission => new Claim(CustomClaimTypes.Permission, permission)));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IHost> StartHostAsync(ISender sender)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHttpContextAccessor();
                    services.AddControllers().AddApplicationPart(typeof(TasksController).Assembly);
                    services.AddSingleton<IOptions<JwtSettings>>(Options.Create(CreateJwtSettings()));
                    services.AddJwtAuthentication();
                    services.AddSingleton(sender);
                    services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();
    }

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

    private static BriefingTaskDto SampleTask(Guid id, Guid briefingId) => new(
        id,
        briefingId,
        "Arte para o post de lançamento",
        DesignerId: null,
        Deadline: null,
        Priority.Medium,
        BriefingTaskStatus.NotStarted);

    private static MultipartFormDataContent FileUpload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);

        return content;
    }

    [Fact]
    public async Task Create_WhenCallerHasTasksCreatePermission_ForwardsBatchAndReturnsOk()
    {
        // Arrange
        var briefingId = Guid.NewGuid();
        CreateBriefingTasksCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingTasksCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<IReadOnlyList<BriefingTaskDto>>>, CancellationToken>((c, _) => capturedCommand = (CreateBriefingTasksCommand)c)
            .ReturnsAsync(Result.Success<IReadOnlyList<BriefingTaskDto>>([SampleTask(Guid.NewGuid(), briefingId)]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{briefingId}/tasks",
            new CreateBriefingTasksRequest([new CreateBriefingTaskItemRequest("Arte para o post de lançamento", Priority.High)]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.BriefingId.Should().Be(briefingId);
        capturedCommand.Tasks.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CreateBriefingTaskItem("Arte para o post de lançamento", Priority.High));
    }

    [Fact]
    public async Task Create_WhenCallerLacksTasksCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/tasks",
            new CreateBriefingTasksRequest([new CreateBriefingTaskItemRequest("Arte", Priority.Medium)]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CreateBriefingTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenBriefingIsNotApproved_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<IReadOnlyList<BriefingTaskDto>>("Task creation not allowed: the briefing is in AwaitingApproval."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/tasks",
            new CreateBriefingTasksRequest([new CreateBriefingTaskItemRequest("Arte", Priority.Medium)]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Task creation not allowed: the briefing is in AwaitingApproval.");
    }

    [Fact]
    public async Task Create_WhenBatchIsEmpty_ReturnsValidationProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<CreateBriefingTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<BriefingTaskDto>>(
                [new ValidationError("Tasks", "At least one task is required.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/briefings/{Guid.NewGuid()}/tasks",
            new CreateBriefingTasksRequest([]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Tasks");
    }

    [Fact]
    public async Task Assign_WhenCallerHasTasksAssignPermission_ForwardsDesignerAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        AssignTaskCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingTaskDto>>, CancellationToken>((c, _) => capturedCommand = (AssignTaskCommand)c)
            .ReturnsAsync(Result.Success(SampleTask(taskId, Guid.NewGuid()) with { DesignerId = designerId }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Assign]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{taskId}/assign", new AssignTaskRequest(designerId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
        capturedCommand.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public async Task Assign_WhenCallerLacksTasksAssignPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}/assign", new AssignTaskRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assign_WhenTaskIsAlreadyAssigned_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<AssignTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingTaskDto>("Task is already assigned."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Assign]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}/assign", new AssignTaskRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Task is already assigned.");
    }

    [Fact]
    public async Task Reassign_WhenCallerHasTasksReassignPermission_ForwardsDesignerAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        ReassignTaskCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ReassignTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingTaskDto>>, CancellationToken>((c, _) => capturedCommand = (ReassignTaskCommand)c)
            .ReturnsAsync(Result.Success(SampleTask(taskId, Guid.NewGuid()) with { DesignerId = designerId }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Reassign]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{taskId}/reassign", new ReassignTaskRequest(designerId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
        capturedCommand.DesignerId.Should().Be(designerId);
    }

    [Fact]
    public async Task Reassign_WhenCallerOnlyHasTasksAssignPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Assign]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}/reassign", new ReassignTaskRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<ReassignTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reassign_WhenTaskIsApproved_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ReassignTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingTaskDto>(
                "Reassignment not allowed: the task is in Approved. Only tasks that are not yet approved can be reassigned."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Reassign]));

        // Act
        var response = await client.PutAsJsonAsync($"/tasks/{Guid.NewGuid()}/reassign", new ReassignTaskRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be(
            "Reassignment not allowed: the task is in Approved. Only tasks that are not yet approved can be reassigned.");
    }

    [Fact]
    public async Task SetDeadlineAndPriority_WhenCallerHasTasksCreatePermission_ForwardsBothAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var deadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        SetDeadlinePriorityCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SetDeadlinePriorityCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingTaskDto>>, CancellationToken>((c, _) => capturedCommand = (SetDeadlinePriorityCommand)c)
            .ReturnsAsync(Result.Success(SampleTask(taskId, Guid.NewGuid()) with { Deadline = deadline, Priority = Priority.Urgent }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{taskId}/deadline-priority",
            new SetDeadlinePriorityRequest(deadline, Priority.Urgent));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
        capturedCommand.Deadline.Should().Be(deadline);
        capturedCommand.Priority.Should().Be(Priority.Urgent);
    }

    [Fact]
    public async Task SetDeadlineAndPriority_WhenDeadlineIsInThePast_ReturnsValidationProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SetDeadlinePriorityCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BriefingTaskDto>(
                [new ValidationError("Deadline", "Task deadline cannot be in the past.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{Guid.NewGuid()}/deadline-priority",
            new SetDeadlinePriorityRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), Priority.Low));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Deadline");
    }

    [Fact]
    public async Task SetDeadlineAndPriority_WhenCallerLacksTasksCreatePermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), roles: [Roles.Designer]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{Guid.NewGuid()}/deadline-priority",
            new SetDeadlinePriorityRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), Priority.Low));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<SetDeadlinePriorityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_WhenCallerHasUpdateStatusPermission_ForwardsStatusAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        UpdateTaskStatusCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingTaskDto>>, CancellationToken>((c, _) => capturedCommand = (UpdateTaskStatusCommand)c)
            .ReturnsAsync(Result.Success(SampleTask(taskId, Guid.NewGuid()) with { Status = BriefingTaskStatus.InProgress }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.UpdateStatus]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{taskId}/status",
            new UpdateTaskStatusRequest(BriefingTaskStatus.InProgress));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
        capturedCommand.Status.Should().Be(BriefingTaskStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatus_WhenCallerIsNotTheAssignee_ReturnsForbiddenProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<BriefingTaskDto>(
                "Only the assigned designer or a design manager can update the status of this task."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.UpdateStatus]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{Guid.NewGuid()}/status",
            new UpdateTaskStatusRequest(BriefingTaskStatus.InProgress));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateStatus_WhenCallerLacksUpdateStatusPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Assign]));

        // Act
        var response = await client.PutAsJsonAsync(
            $"/tasks/{Guid.NewGuid()}/status",
            new UpdateTaskStatusRequest(BriefingTaskStatus.Blocked));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<UpdateTaskStatusCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reopen_WhenCallerHasReopenPermission_ForwardsTaskIdAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        ReopenTaskCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ReopenTaskCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<BriefingTaskDto>>, CancellationToken>((c, _) => capturedCommand = (ReopenTaskCommand)c)
            .ReturnsAsync(Result.Success(SampleTask(taskId, Guid.NewGuid()) with { Status = BriefingTaskStatus.InProgress }));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Reopen]));

        // Act
        var response = await client.PostAsync($"/tasks/{taskId}/reopen", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task Reopen_WhenCallerLacksReopenPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.UpdateStatus]));

        // Act
        var response = await client.PostAsync($"/tasks/{Guid.NewGuid()}/reopen", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<ReopenTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reopen_WhenTaskIsNotApproved_ReturnsConflictProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ReopenTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BriefingTaskDto>(
                "Reopening not allowed: the task is in InProgress. Only approved tasks can be reopened."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Reopen]));

        // Act
        var response = await client.PostAsync($"/tasks/{Guid.NewGuid()}/reopen", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterComment_WhenCallerHasCommentPermission_ForwardsTextAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        RegisterCommentCommand? capturedCommand = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RegisterCommentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CommentDto>>, CancellationToken>((c, _) => capturedCommand = (RegisterCommentCommand)c)
            .ReturnsAsync(Result.Success(new CommentDto(
                Guid.NewGuid(), taskId, Guid.NewGuid(), "Aguardando o logo em vetor.", DateTime.UtcNow)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Comment]));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/tasks/{taskId}/comments",
            new RegisterCommentRequest("Aguardando o logo em vetor."));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedCommand.Should().NotBeNull();
        capturedCommand!.TaskId.Should().Be(taskId);
        capturedCommand.Text.Should().Be("Aguardando o logo em vetor.");
    }

    [Fact]
    public async Task RegisterComment_WhenTextIsEmpty_ReturnsValidationProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<RegisterCommentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CommentDto>([new ValidationError("Text", "Comment text is required.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Comment]));

        // Act
        var response = await client.PostAsJsonAsync($"/tasks/{Guid.NewGuid()}/comments", new RegisterCommentRequest(null));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Text");
    }

    [Fact]
    public async Task RegisterComment_WhenCallerLacksCommentPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Create]));

        // Act
        var response = await client.PostAsJsonAsync($"/tasks/{Guid.NewGuid()}/comments", new RegisterCommentRequest("Texto"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<RegisterCommentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitDelivery_WhenFilePartIsPresent_ForwardsFileMetadataAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var bytes = "conteudo-da-entrega"u8.ToArray();
        string? capturedFileName = null;
        long capturedSize = 0;
        var capturedHasContent = false;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitTaskDeliveryCommand>(), It.IsAny<CancellationToken>()))
            // Read inside the callback: the request stream is disposed once the response completes.
            .Callback<IRequest<Result<AttachmentDto>>, CancellationToken>((c, _) =>
            {
                var command = (SubmitTaskDeliveryCommand)c;
                capturedFileName = command.FileName;
                capturedSize = command.SizeBytes;
                capturedHasContent = command.Content is not null;
            })
            .ReturnsAsync(Result.Success(new AttachmentDto(
                Guid.NewGuid(), Guid.NewGuid(), taskId, "entrega.pdf", bytes.Length, Guid.NewGuid(), DateTime.UtcNow)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Deliver]));

        // Act
        using var upload = FileUpload("entrega.pdf", bytes);
        var response = await client.PostAsync($"/tasks/{taskId}/deliveries", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedFileName.Should().Be("entrega.pdf");
        capturedSize.Should().Be(bytes.Length);
        capturedHasContent.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitDelivery_WhenFilePartIsMissing_DispatchesEmptyCommandAndReturnsValidationProblem()
    {
        // Arrange
        var capturedHasContent = true;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitTaskDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AttachmentDto>>, CancellationToken>((c, _) =>
                capturedHasContent = ((SubmitTaskDeliveryCommand)c).Content is not null)
            .ReturnsAsync(Result.Failure<AttachmentDto>([new ValidationError("Content", "File content is required.")]));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Deliver]));

        // Act — a multipart body carrying everything but the file part.
        using var upload = new MultipartFormDataContent { { new StringContent("entrega"), "kind" } };
        var response = await client.PostAsync($"/tasks/{Guid.NewGuid()}/deliveries", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        capturedHasContent.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitDelivery_WhenCallerLacksDeliverPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Comment]));

        // Act
        using var upload = FileUpload("entrega.pdf", "conteudo"u8.ToArray());
        var response = await client.PostAsync($"/tasks/{Guid.NewGuid()}/deliveries", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<SubmitTaskDeliveryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOverdue_WhenCallerHasViewAllPermission_ForwardsQueryWithDefaultPagingAndReturnsOk()
    {
        // Arrange
        GetOverdueTasksQuery? capturedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetOverdueTasksQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<OverdueTaskDto>>>, CancellationToken>((q, _) => capturedQuery = (GetOverdueTasksQuery)q)
            .ReturnsAsync(Result.Success(PagedDto<OverdueTaskDto>.Create([], 1, 20, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAll]));

        // Act
        var response = await client.GetAsync("/tasks/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetOverdue_WhenPagingIsGiven_ForwardsIt()
    {
        // Arrange
        GetOverdueTasksQuery? capturedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetOverdueTasksQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedDto<OverdueTaskDto>>>, CancellationToken>((q, _) => capturedQuery = (GetOverdueTasksQuery)q)
            .ReturnsAsync(Result.Success(PagedDto<OverdueTaskDto>.Create([], 3, 50, 0)));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAll]));

        // Act
        var response = await client.GetAsync("/tasks/overdue?page=3&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery!.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetOverdue_WhenCallerHasOnlyViewAssignedPermission_ReturnsForbidden()
    {
        // Arrange — the delay list crosses every designer's workload, which is exactly what the
        // assignee-scoped permission does not open.
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAssigned]));

        // Act
        var response = await client.GetAsync("/tasks/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetOverdueTasksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TaskDetailDto SampleTaskDetail(Guid id, Guid briefingId) => new(
        id,
        briefingId,
        "Campanha de verão",
        "Arte para o post de lançamento",
        DesignerId: null,
        DesignerName: null,
        Deadline: null,
        Priority.Medium,
        BriefingTaskStatus.NotStarted,
        Comments: [],
        Deliveries: []);

    [Fact]
    public async Task GetById_WhenCallerHasViewAssignedPermission_ForwardsQueryAndReturnsOk()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        GetTaskDetailQuery? capturedQuery = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetTaskDetailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<TaskDetailDto>>, CancellationToken>((q, _) => capturedQuery = (GetTaskDetailQuery)q)
            .ReturnsAsync(Result.Success(SampleTaskDetail(taskId, Guid.NewGuid())));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAssigned]));

        // Act
        var response = await client.GetAsync($"/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.TaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task GetById_WhenCallerHasOnlyViewAllPermission_ReturnsOk()
    {
        // Arrange — the Design Manager holds tasks.view-all and not tasks.view-assigned (USR-047); either
        // one opens this read (BRF-034).
        var taskId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetTaskDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(SampleTaskDetail(taskId, Guid.NewGuid())));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAll]));

        // Act
        var response = await client.GetAsync($"/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_WhenCallerHasNoTaskViewPermission_ReturnsForbidden()
    {
        // Arrange
        var senderMock = new Mock<ISender>();

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Comment]));

        // Act
        var response = await client.GetAsync($"/tasks/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<GetTaskDetailQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenCallerIsNotInvolvedInTheTask_ReturnsForbiddenProblem()
    {
        // Arrange — permission opens the door, involvement is the handler's answer.
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetTaskDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<TaskDetailDto>("Only the people involved in the task can view it."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.ViewAssigned]));

        // Act
        var response = await client.GetAsync($"/tasks/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Only the people involved in the task can view it.");
    }

    [Fact]
    public async Task SubmitDelivery_WhenTaskDoesNotExist_ReturnsNotFoundProblem()
    {
        // Arrange
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitTaskDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<AttachmentDto>("Task not found."));

        using var host = await StartHostAsync(senderMock.Object);
        var client = host.GetTestClient();
        Authenticate(client, GenerateToken(CreateJwtSettings(), Guid.NewGuid(), permissions: [Permissions.Tasks.Deliver]));

        // Act
        using var upload = FileUpload("entrega.pdf", "conteudo"u8.ToArray());
        var response = await client.PostAsync($"/tasks/{Guid.NewGuid()}/deliveries", upload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Task not found.");
    }
}
