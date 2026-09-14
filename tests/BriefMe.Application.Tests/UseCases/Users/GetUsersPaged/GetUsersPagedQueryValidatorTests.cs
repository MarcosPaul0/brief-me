using BriefMe.Application.UseCases.Users.GetUsersPaged;
using BriefMe.Domain.Repositories;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BriefMe.Application.Tests.UseCases.Users.GetUsersPaged;

public class GetUsersPagedQueryValidatorTests
{
    private readonly GetUsersPagedQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_HasNoErrors()
    {
        // Arrange
        var query = new GetUsersPagedQuery(Search: null, Status: null, RoleId: null, Page: 1, PageSize: 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPageIsLessThanOne_HasErrorForPage()
    {
        // Arrange
        var query = new GetUsersPagedQuery(Search: null, Status: null, RoleId: null, Page: 0, PageSize: 10);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfRange_HasErrorForPageSize(int pageSize)
    {
        // Arrange
        var query = new GetUsersPagedQuery(Search: null, Status: null, RoleId: null, Page: 1, PageSize: pageSize);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_WhenSortFieldIsInvalid_HasErrorForSortField()
    {
        // Arrange
        var query = new GetUsersPagedQuery(
            Search: null,
            Status: null,
            RoleId: null,
            Page: 1,
            PageSize: 10,
            SortField: (UserSortField)999);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SortField);
    }
}
