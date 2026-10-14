using BriefMe.Domain.Entities;
using BriefMe.Domain.Enums;
using BriefMe.Domain.Exceptions;
using FluentAssertions;

namespace BriefMe.Domain.Tests.Entities;

public class BriefingTests
{
    private static readonly Guid DesignerId = Guid.NewGuid();

    private static Briefing NewDraft() =>
        Briefing.Create("Launch campaign", Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

    private static Briefing CompleteDraft() =>
        Briefing.Create(
            "Launch campaign",
            Priority.High,
            Guid.NewGuid(),
            Guid.NewGuid(),
            description: "Social media assets",
            objective: "Promote the new product",
            requestingArea: "Marketing",
            desiredDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

    private static Briefing Submitted()
    {
        var briefing = CompleteDraft();
        briefing.Submit();
        return briefing;
    }

    private static Briefing InProgress()
    {
        var briefing = Submitted();
        briefing.Claim(DesignerId);
        return briefing;
    }

    private static Briefing Delivered()
    {
        var briefing = InProgress();
        briefing.Deliver(DesignerId, "https://files.example/v1");
        return briefing;
    }

    private static Briefing Completed()
    {
        var briefing = Delivered();
        briefing.Accept();
        return briefing;
    }

    private static Briefing Cancelled()
    {
        var briefing = CompleteDraft();
        briefing.Cancel();
        return briefing;
    }

    private static Briefing InStatus(BriefingStatus status) => status switch
    {
        BriefingStatus.Draft => CompleteDraft(),
        BriefingStatus.Submitted => Submitted(),
        BriefingStatus.InProgress => InProgress(),
        BriefingStatus.Delivered => Delivered(),
        BriefingStatus.Completed => Completed(),
        BriefingStatus.Cancelled => Cancelled(),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    // --- Create ---------------------------------------------------------------------------------------------

    [Fact]
    public void Create_WhenDataIsValid_StartsAsDraftWithoutDesigner()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        // Act
        var briefing = Briefing.Create("New banner", Priority.Low, categoryId, requesterId);

        // Assert
        briefing.Should().BeEquivalentTo(new
        {
            Status = BriefingStatus.Draft,
            CategoryId = categoryId,
            RequesterId = requesterId,
            DesignerId = (Guid?)null
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenTitleIsNullOrWhitespace_ThrowsDomainException(string? title)
    {
        // Arrange
        // (none)

        // Act
        var act = () => Briefing.Create(title!, Priority.Medium, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenCategoryIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Briefing.Create("Banner", Priority.Medium, Guid.Empty, Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhenRequesterIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        // (none)

        // Act
        var act = () => Briefing.Create("Banner", Priority.Medium, Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Edit -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Submitted)]
    public void Edit_WhenNobodyIsWorkingOnIt_UpdatesContent(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        briefing.Edit("Edited title", Priority.Urgent, "New description", "New objective", "Design", null);

        // Assert
        briefing.Should().BeEquivalentTo(new { Title = "Edited title", Priority = Priority.Urgent, Objective = "New objective" });
    }

    [Theory]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Delivered)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Edit_WhenClaimedOrClosed_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Edit("x", Priority.Medium, null, null, null, null);

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Submit ---------------------------------------------------------------------------------------------

    [Fact]
    public void Submit_WhenAllRequiredFieldsPresent_MovesToSubmitted()
    {
        // Arrange
        var briefing = CompleteDraft();

        // Act
        var result = briefing.Submit();

        // Assert
        result.IsSuccess.Should().BeTrue();
        briefing.Status.Should().Be(BriefingStatus.Submitted);
    }

    [Fact]
    public void Submit_WhenRequiredFieldsMissing_ReturnsFieldErrorsAndKeepsDraft()
    {
        // Arrange
        var briefing = NewDraft();

        // Act
        var result = briefing.Submit();

        // Assert
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            nameof(Briefing.Description), nameof(Briefing.Objective), nameof(Briefing.RequestingArea), nameof(Briefing.DesiredDeliveryDate));
        briefing.Status.Should().Be(BriefingStatus.Draft);
    }

    [Theory]
    [InlineData(BriefingStatus.Submitted)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Submit_WhenNotADraft_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Submit();

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Cancel ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.Submitted)]
    public void Cancel_WhenNobodyIsWorkingOnIt_MovesToCancelled(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        briefing.Cancel();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Cancelled);
    }

    [Theory]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Delivered)]
    [InlineData(BriefingStatus.Completed)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Cancel_WhenClaimedOrClosed_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Cancel();

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Claim ----------------------------------------------------------------------------------------------

    [Fact]
    public void Claim_WhenSubmitted_AssignsDesignerAndMovesToInProgress()
    {
        // Arrange
        var briefing = Submitted();

        // Act
        briefing.Claim(DesignerId);

        // Assert
        briefing.Should().BeEquivalentTo(new { DesignerId = (Guid?)DesignerId, Status = BriefingStatus.InProgress });
    }

    [Theory]
    [InlineData(BriefingStatus.Draft)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Delivered)]
    [InlineData(BriefingStatus.Cancelled)]
    public void Claim_WhenNotInTheQueue_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Claim(Guid.NewGuid());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Claim_WhenDesignerIdIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var briefing = Submitted();

        // Act
        var act = () => briefing.Claim(Guid.Empty);

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Deliver --------------------------------------------------------------------------------------------

    [Fact]
    public void Deliver_WhenAssigneeDeliversWithNote_StoresTrimmedNoteAndMovesToDelivered()
    {
        // Arrange
        var briefing = InProgress();

        // Act
        briefing.Deliver(DesignerId, "  https://files.example/v1  ");

        // Assert
        briefing.Should().BeEquivalentTo(new { DeliveryNote = "https://files.example/v1", Status = BriefingStatus.Delivered });
    }

    [Fact]
    public void Deliver_WhenSuccessful_StampsDeliveredAt()
    {
        // Arrange
        var briefing = InProgress();

        // Act
        briefing.Deliver(DesignerId, "https://files.example/v1");

        // Assert
        briefing.DeliveredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deliver_WhenAnswersAChangeRequest_ClearsTheReason()
    {
        // Arrange
        var briefing = Delivered();
        briefing.RequestChanges("Use the new logo");

        // Act
        briefing.Deliver(DesignerId, "https://files.example/v2");

        // Assert
        briefing.ChangeRequestReason.Should().BeNull();
    }

    [Fact]
    public void Deliver_WhenCallerIsNotTheAssignee_ThrowsDomainException()
    {
        // Arrange
        var briefing = InProgress();

        // Act
        var act = () => briefing.Deliver(Guid.NewGuid(), "https://files.example/v1");

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Deliver_WhenNoteIsBlank_ThrowsDomainException(string? note)
    {
        // Arrange
        var briefing = InProgress();

        // Act
        var act = () => briefing.Deliver(DesignerId, note);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(BriefingStatus.Submitted)]
    [InlineData(BriefingStatus.Delivered)]
    [InlineData(BriefingStatus.Completed)]
    public void Deliver_WhenNotInProgress_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Deliver(DesignerId, "https://files.example/v1");

        // Assert
        act.Should().Throw<DomainException>();
    }

    // --- Accept / RequestChanges ----------------------------------------------------------------------------

    [Fact]
    public void Accept_WhenDelivered_MovesToCompleted()
    {
        // Arrange
        var briefing = Delivered();

        // Act
        briefing.Accept();

        // Assert
        briefing.Status.Should().Be(BriefingStatus.Completed);
    }

    [Theory]
    [InlineData(BriefingStatus.Submitted)]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    public void Accept_WhenNotDelivered_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.Accept();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RequestChanges_WhenDelivered_ReturnsToSameDesigner()
    {
        // Arrange
        var briefing = Delivered();

        // Act
        briefing.RequestChanges("Use the new logo");

        // Assert
        briefing.Should().BeEquivalentTo(new { Status = BriefingStatus.InProgress, DesignerId = (Guid?)DesignerId });
    }

    [Fact]
    public void RequestChanges_WhenDelivered_KeepsTheReason()
    {
        // Arrange
        var briefing = Delivered();

        // Act
        briefing.RequestChanges("  Use the new logo ");

        // Assert
        briefing.ChangeRequestReason!.Value.Should().Be("Use the new logo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RequestChanges_WhenReasonIsBlank_ThrowsAndKeepsDelivered(string? reason)
    {
        // Arrange
        var briefing = Delivered();

        // Act
        var act = () => briefing.RequestChanges(reason);

        // Assert
        act.Should().Throw<DomainException>();
        briefing.Status.Should().Be(BriefingStatus.Delivered);
    }

    [Theory]
    [InlineData(BriefingStatus.InProgress)]
    [InlineData(BriefingStatus.Completed)]
    public void RequestChanges_WhenNotDelivered_ThrowsDomainException(BriefingStatus status)
    {
        // Arrange
        var briefing = InStatus(status);

        // Act
        var act = () => briefing.RequestChanges("Use the new logo");

        // Assert
        act.Should().Throw<DomainException>();
    }
}
