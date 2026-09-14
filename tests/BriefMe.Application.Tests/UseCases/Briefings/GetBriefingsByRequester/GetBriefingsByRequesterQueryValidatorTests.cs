using BriefMe.Application.UseCases.Briefings.GetBriefingsByRequester;
using BriefMe.Domain.Enums;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Briefings.GetBriefingsByRequester;

public class GetBriefingsByRequesterQueryValidatorTests
{
    private readonly GetBriefingsByRequesterQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenPageAndPageSizeValid_IsValid()
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageIsLessThanOne_IsInvalid(int page)
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: page, PageSize: 20);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetBriefingsByRequesterQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeOutOfRange_IsInvalid(int pageSize)
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetBriefingsByRequesterQuery.PageSize));
    }

    [Fact]
    public void Validate_WhenStatusIsUndefinedEnumValue_IsInvalid()
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 20, Status: (BriefingStatus)99);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetBriefingsByRequesterQuery.Status));
    }

    [Fact]
    public void Validate_WhenStatusIsNull_IsValid()
    {
        // Arrange
        var query = new GetBriefingsByRequesterQuery(Page: 1, PageSize: 20, Status: null);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
