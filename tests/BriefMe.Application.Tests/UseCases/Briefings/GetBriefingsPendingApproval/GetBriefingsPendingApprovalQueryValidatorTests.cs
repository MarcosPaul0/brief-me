using BriefMe.Application.UseCases.Briefings.GetBriefingsPendingApproval;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingsPendingApproval;

public class GetBriefingsPendingApprovalQueryValidatorTests
{
    private readonly GetBriefingsPendingApprovalQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenPageAndPageSizeAreValid_Passes()
    {
        // Arrange
        var query = new GetBriefingsPendingApprovalQuery(1, 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageIsLessThanOne_Fails(int page)
    {
        // Arrange
        var query = new GetBriefingsPendingApprovalQuery(page, 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetBriefingsPendingApprovalQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfRange_Fails(int pageSize)
    {
        // Arrange
        var query = new GetBriefingsPendingApprovalQuery(1, pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetBriefingsPendingApprovalQuery.PageSize));
    }
}
