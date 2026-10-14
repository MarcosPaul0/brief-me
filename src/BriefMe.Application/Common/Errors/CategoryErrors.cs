namespace BriefMe.Application.Common.Errors;

public static class CategoryErrors
{
    public const string NotFound = "Category not found.";

    public const string NotFoundOrInactive = "Category not found or inactive.";

    public const string DuplicateName = "A category with this name already exists.";

    public static class Validation
    {
        public const string CategoryRequired = "Category is required.";

        public const string NameRequired = "Name is required.";

        public static string NameTooLong(int maxLength) => $"Name must be at most {maxLength} characters.";

        public static string DescriptionTooLong(int maxLength) => $"Description must be at most {maxLength} characters.";
    }
}
