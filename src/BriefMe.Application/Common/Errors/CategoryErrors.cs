namespace BriefMe.Application.Common.Errors;

/// <summary>Failure messages for the category lookups used when creating or editing a briefing.</summary>
public static class CategoryErrors
{
    public const string NotFoundOrInactive = "Category not found or inactive.";

    public static class Validation
    {
        public const string CategoryRequired = "Category is required.";
    }
}
