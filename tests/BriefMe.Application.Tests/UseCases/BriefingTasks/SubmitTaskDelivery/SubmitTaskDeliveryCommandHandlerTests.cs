using System.Text;
using BriefMe.Application.Abstractions;
using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.BriefingTasks.SubmitTaskDelivery;
using BriefMe.Domain.Common;
using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Repositories;
using BriefMe.Domain.ValueObjects;
using FluentAssertions;
using Moq;

using DomainRoles = BriefMe.Domain.Constants.Roles;

namespace BriefMe.Application.Tests.UseCases.BriefingTasks.SubmitTaskDelivery;

public class SubmitTaskDeliveryCommandHandlerTests
{
    private const string StorageKey = "2026/07/29/0f3a1c.psd";

    private readonly Mock<IBriefingTaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IBriefingRepository> _briefingRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IFileValidator> _fileValidatorMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Role _designerRole = Role.Create(DomainRoles.Designer, "Execução das tarefas.", isSystemRole: true);
    private readonly Role _managerRole = Role.Create(DomainRoles.GestorDeDesign, "Gestão do design.", isSystemRole: true);
    private readonly Role _adminRole = Role.Create(DomainRoles.Administrador, "Acesso total.", isSystemRole: true);

    private readonly Guid _assigneeId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly SubmitTaskDeliveryCommandHandler _handler;

    private Briefing _briefing = null!;

    public SubmitTaskDeliveryCommandHandlerTests()
    {
        _handler = new SubmitTaskDeliveryCommandHandler(
            _taskRepositoryMock.Object,
            _briefingRepositoryMock.Object,
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _fileValidatorMock.Object,
            _fileStorageMock.Object,
            _currentUserMock.Object,
            _unitOfWorkMock.Object);

        // Default caller: the designer the task was assigned to — the common RF022 path.
        _currentUserMock.Setup(u => u.UserId).Returns(_assigneeId.ToString());

        _roleRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_designerRole, _managerRole, _adminRole]);

        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Success());

        _fileStorageMock
            .Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageKey);
    }

    private static SubmitTaskDeliveryCommand Command(Guid taskId, string fileName = "key-visual.psd") =>
        new(taskId, fileName, 4096, new MemoryStream(Encoding.UTF8.GetBytes("bytes")));

    /// <summary>
    /// A task assigned to <paramref name="designerId"/>, walked to <paramref name="status"/> through
    /// legal transitions only, whose parent briefing is under execution — the state a briefing is in once it
    /// has been broken down into tasks (RF015).
    /// </summary>
    private BriefingTask ArrangeTask(
        Guid? designerId = null,
        BriefingTaskStatus status = BriefingTaskStatus.InProgress,
        BriefingStatus briefingStatus = BriefingStatus.InProgress)
    {
        _briefing = Briefing.Create(
            "Campanha de fim de ano",
            Priority.Medium,
            Guid.NewGuid(),
            _requesterId);

        ForceBriefingStatus(_briefing, briefingStatus);

        var task = BriefingTask.Create(
            _briefing.Id,
            "Key visual",
            Priority.Medium,
            designerId ?? _assigneeId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        foreach (var step in PathTo(status))
            task.UpdateStatus(step);

        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(_briefing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_briefing);

        return task;
    }

    private static void ForceBriefingStatus(Briefing briefing, BriefingStatus status)
    {
        typeof(Briefing)
            .GetProperty(nameof(Briefing.Status))!
            .SetValue(briefing, status);
    }

    private static BriefingTaskStatus[] PathTo(BriefingTaskStatus status) => status switch
    {
        BriefingTaskStatus.NotStarted => [],
        BriefingTaskStatus.InProgress => [BriefingTaskStatus.InProgress],
        BriefingTaskStatus.Blocked => [BriefingTaskStatus.Blocked],
        BriefingTaskStatus.AwaitingTechnicalApproval =>
            [BriefingTaskStatus.InProgress, BriefingTaskStatus.AwaitingTechnicalApproval],
        BriefingTaskStatus.Rejected =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.Rejected
        ],
        BriefingTaskStatus.AwaitingRequesterApproval =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval
        ],
        BriefingTaskStatus.Approved =>
        [
            BriefingTaskStatus.InProgress,
            BriefingTaskStatus.AwaitingTechnicalApproval,
            BriefingTaskStatus.AwaitingRequesterApproval,
            BriefingTaskStatus.Approved
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped status.")
    };

    /// <summary>Makes the authenticated caller a user holding <paramref name="roleIds"/>.</summary>
    private Guid ArrangeCaller(params Guid[] roleIds)
    {
        var user = User.Create("Ana Silva", Email.Create("ana@briefme.com").Value, "hashed-password");

        foreach (var roleId in roleIds)
            user.AssignRole(roleId);

        _currentUserMock.Setup(u => u.UserId).Returns(user.Id.ToString());

        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        return user.Id;
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_RecordsTheDelivery()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("key-visual.psd");
        result.Value.SizeBytes.Should().Be(4096);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_LinksTheTaskAndItsBriefing()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Value.TaskId.Should().Be(task.Id);
        result.Value.BriefingId.Should().Be(task.BriefingId);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_AttachesItToTheBriefingAggregate()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        _briefing.Attachments.Should().ContainSingle(a => a.Id == result.Value.Id);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_StoresTheBytesAndKeepsTheReturnedKey()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), "key-visual.psd", It.IsAny<CancellationToken>()),
            Times.Once);
        _briefing.Attachments.Single().StoragePath.Should().Be(StorageKey);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_CommitsOnce()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_TakesTheAuthorFromTheAuthenticatedUser()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Value.UploadedById.Should().Be(_assigneeId);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_StampsTheUploadMoment()
    {
        // Arrange
        var task = ArrangeTask();
        var before = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Value.UploadedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WhenDeliveryIsRecorded_LeavesTheTaskAndTheBriefingUnchanged()
    {
        // Arrange
        var task = ArrangeTask();
        var deadline = task.Deadline;

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        task.Status.Should().Be(BriefingTaskStatus.InProgress);
        task.DesignerId.Should().Be(_assigneeId);
        task.Deadline.Should().Be(deadline);
        task.Priority.Should().Be(Priority.Medium);
        _briefing.Status.Should().Be(BriefingStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenASecondDeliveryIsSubmitted_KeepsBoth()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(Command(task.Id, "v1.psd"), default);
        await _handler.Handle(Command(task.Id, "v2.psd"), default);

        // Assert
        _briefing.Attachments.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(BriefingTaskStatus.NotStarted)]
    [InlineData(BriefingTaskStatus.InProgress)]
    [InlineData(BriefingTaskStatus.Blocked)]
    [InlineData(BriefingTaskStatus.AwaitingTechnicalApproval)]
    [InlineData(BriefingTaskStatus.AwaitingRequesterApproval)]
    [InlineData(BriefingTaskStatus.Rejected)]
    public async Task Handle_WhenTaskIsNotApproved_RecordsTheDelivery(BriefingTaskStatus status)
    {
        // Arrange
        var task = ArrangeTask(status: status);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_ReturnsConflict()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.Approved);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingTaskErrors.DeliveryNotAllowed(BriefingTaskStatus.Approved));
    }

    [Fact]
    public async Task Handle_WhenTaskIsApproved_DoesNotStoreNorCommit()
    {
        // Arrange
        var task = ArrangeTask(status: BriefingTaskStatus.Approved);

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.AwaitingApproval)]
    [InlineData(BriefingStatus.ChangesRequested)]
    [InlineData(BriefingStatus.Approved)]
    [InlineData(BriefingStatus.Rejected)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public async Task Handle_WhenBriefingIsNotInProgress_ReturnsConflict(BriefingStatus status)
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: status);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be(BriefingErrors.DeliveryNotAllowed(status));
    }

    [Fact]
    public async Task Handle_WhenBriefingIsNotInProgress_DoesNotStoreNorCommit()
    {
        // Arrange
        var task = ArrangeTask(briefingStatus: BriefingStatus.Cancelled);

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheAssignee_DoesNotLookUpProfiles()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _userRepositoryMock.Verify(
            r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsADesignManager_RecordsTheDelivery()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_managerRole.Id);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAnAdministrator_RecordsTheDelivery()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_adminRole.Id);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerIsAnotherDesigner_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotAssignedToDeliver);
    }

    [Fact]
    public async Task Handle_WhenCallerIsTheBriefingRequester_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        _currentUserMock.Setup(u => u.UserId).Returns(_requesterId.ToString());
        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(_requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenCallerMayNotDeliver_DoesNotStoreNorCommit()
    {
        // Arrange
        var task = ArrangeTask();
        ArrangeCaller(_designerRole.Id);

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssigneeAndCallerIsNotManagement_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask(designerId: Guid.Empty);
        ArrangeCaller(_designerRole.Id);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        task.DesignerId.Should().BeNull();
        result.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenTaskHasNoAssigneeAndCallerIsAManager_RecordsTheDelivery()
    {
        // Arrange
        var task = ArrangeTask(designerId: Guid.Empty);
        ArrangeCaller(_managerRole.Id);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenCallerNoLongerExists_ReturnsForbidden()
    {
        // Arrange
        var task = ArrangeTask();
        _currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid().ToString());
        _userRepositoryMock
            .Setup(r => r.GetByIdWithRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Should().Be(BriefingTaskErrors.NotAssignedToDeliver);
    }

    [Fact]
    public async Task Handle_WhenFileValidationFails_ReturnsTheValidatorErrors()
    {
        // Arrange
        var task = ArrangeTask();
        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Failure(
            [
                new ValidationError("sizeBytes", "File exceeds the maximum allowed size of 100 bytes."),
                new ValidationError("fileName", "File type '.exe' is not allowed.")
            ]));

        // Act
        var result = await _handler.Handle(Command(task.Id, "payload.exe"), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenFileValidationFails_DoesNotStoreNorCommit()
    {
        // Arrange
        var task = ArrangeTask();
        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Failure([new ValidationError("sizeBytes", "File must not be empty.")]));

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileValidationFails_DoesNotLoadTheTask()
    {
        // Arrange
        var task = ArrangeTask();
        _fileValidatorMock
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<long>()))
            .Returns(Result.Failure([new ValidationError("sizeBytes", "File must not be empty.")]));

        // Act
        await _handler.Handle(Command(task.Id), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenContentIsNull_ReturnsValidationFailure()
    {
        // Arrange
        var task = ArrangeTask();

        // Act
        var result = await _handler.Handle(
            new SubmitTaskDeliveryCommand(task.Id, "key-visual.psd", 4096, Content: null),
            default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(BriefingTaskErrors.Validation.FileContentRequired);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        var result = await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingTaskErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_DoesNotStore()
    {
        // Arrange
        _taskRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BriefingTask?)null);

        // Act
        await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenParentBriefingNoLongerExists_ReturnsNotFound()
    {
        // Arrange
        var task = ArrangeTask();
        _briefingRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Briefing?)null);

        // Act
        var result = await _handler.Handle(Command(task.Id), default);

        // Assert
        result.Type.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be(BriefingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAValidGuid_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns("not-a-guid");

        // Act
        var result = await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        result.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Should().Be(CommonErrors.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIsNotAuthenticated_DoesNotTouchRepositoriesNorStorage()
    {
        // Arrange
        _currentUserMock.Setup(u => u.UserId).Returns((string?)null);

        // Act
        await _handler.Handle(Command(Guid.NewGuid()), default);

        // Assert
        _taskRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fileStorageMock.Verify(
            s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
