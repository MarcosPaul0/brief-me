using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Briefings.GetBriefingTracking;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingTracking;

public class GetBriefingTrackingQueryValidatorTests
{
    private readonly GetBriefingTrackingQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenBriefingIdIsProvided_Passes()
    {
        // Arrange
        var query = new GetBriefingTrackingQuery(Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBriefingIdIsEmpty_Fails()
    {
        // Arrange
        var query = new GetBriefingTrackingQuery(Guid.Empty);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(GetBriefingTrackingQuery.BriefingId) &&
            error.ErrorMessage == BriefingErrors.Validation.BriefingRequired);
    }
}
