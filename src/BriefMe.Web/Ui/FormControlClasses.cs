namespace BriefMe.Web.Ui;

/// <summary>
/// Puts a primitive's base classes in front of whatever <c>class</c> the caller passed, inside the
/// <c>AdditionalAttributes</c> Blazor's form components read. Lets the <c>Form*</c> primitives subclass the
/// framework inputs — binding, parsing, EditContext and validation state stay Blazor's own code.
/// </summary>
internal static class FormControlClasses
{
    public static IReadOnlyDictionary<string, object> Prepend(IReadOnlyDictionary<string, object>? attributes, string baseClasses)
    {
        var merged = attributes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(attributes);

        // Idempotent across re-renders: the caller's class is whatever follows our prefix.
        var callerClass = merged.TryGetValue("class", out var existing) ? existing?.ToString() : null;
        if (callerClass is not null && callerClass.StartsWith(baseClasses, StringComparison.Ordinal))
            callerClass = callerClass[baseClasses.Length..];

        merged["class"] = ClassNames.Join(baseClasses, callerClass);
        return merged;
    }
}
