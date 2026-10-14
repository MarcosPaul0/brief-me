namespace BriefMe.Web.Contracts.Categories;

public sealed record CreateCategoryRequest(string Name, string? Description);

public sealed record UpdateCategoryRequest(string Name, string? Description);

public sealed record ChangeCategoryStatusRequest(bool IsActive);
