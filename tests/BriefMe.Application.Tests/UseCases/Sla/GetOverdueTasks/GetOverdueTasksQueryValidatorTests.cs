using BriefMe.Application.UseCases.Sla.GetOverdueTasks;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Sla.GetOverdueTasks;

public class GetOverdueTasksQueryValidatorTests
{
    private readonly GetOverdueTasksQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenPageAndPageSizeAreValid_Passes()
    {
        // Arrange
        var query = new GetOverdueTasksQuery(1, 20);

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
        var query = new GetOverdueTasksQuery(page, 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetOverdueTasksQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfRange_Fails(int pageSize)
    {
        // Arrange
        var query = new GetOverdueTasksQuery(1, pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(GetOverdueTasksQuery.PageSize));
    }
}
