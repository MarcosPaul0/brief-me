namespace BriefMe.Domain.Common;

public sealed record ValidationError(string PropertyName, string ErrorMessage);
