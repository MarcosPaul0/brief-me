using BriefMe.Application.UseCases.Sla.GetOverdueBriefings;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Sla.GetOverdueBriefings;

public class GetOverdueBriefingsQueryValidatorTests
{
    private readonly GetOverdueBriefingsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenPageAndPageSizeAreValid_Passes()
    {
        // Arrange
        var query = new GetOverdueBriefingsQuery(1, 20);

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
        var query = new GetOverdueBriefingsQuery(page, 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetOverdueBriefingsQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfRange_Fails(int pageSize)
    {
        // Arrange
        var query = new GetOverdueBriefingsQuery(1, pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetOverdueBriefingsQuery.PageSize));
    }
}
