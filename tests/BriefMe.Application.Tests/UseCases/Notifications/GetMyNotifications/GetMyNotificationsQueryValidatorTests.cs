using BriefMe.Application.Common.Errors;
using BriefMe.Application.UseCases.Notifications.GetMyNotifications;
using FluentAssertions;

namespace BriefMe.Application.Tests.UseCases.Notifications.GetMyNotifications;

public class GetMyNotificationsQueryValidatorTests
{
    private readonly GetMyNotificationsQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageIsBelowOne_Fails(int page)
    {
        // Arrange
        var query = new GetMyNotificationsQuery(page, 10);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == CommonErrors.Paging.PageOutOfRange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfRange_Fails(int pageSize)
    {
        // Arrange
        var query = new GetMyNotificationsQuery(1, pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == CommonErrors.Paging.PageSizeOutOfRange(100));
    }

    [Fact]
    public void Validate_WhenPagingIsWithinRange_Passes()
    {
        // Arrange
        var query = new GetMyNotificationsQuery(1, 10);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
