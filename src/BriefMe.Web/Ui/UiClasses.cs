namespace BriefMe.Web.Ui;

/// <summary>
/// Class strings shared by more than one primitive, so the plain fields (<see cref="Input"/>, ...) and the
/// EditForm-bound ones (<see cref="FormInput"/>, ...) render identically.
/// </summary>
internal static class UiClasses
{
    /// <summary>
    /// DESIGN-clay.md <c>text-input</c>: cream canvas, hairline rule, 12px corner, 44px tall; focus thickens the
    /// rule to ink (<c>text-input-focused</c>).
    /// </summary>
    public const string Control =
        "min-h-11 w-full rounded-md border border-hairline bg-canvas px-4 py-2.5 text-body-md text-ink " +
        "placeholder:text-muted-soft transition-colors focus:border-ink focus:outline-none " +
        "focus:ring-1 focus:ring-ink disabled:cursor-not-allowed disabled:bg-surface-soft disabled:text-muted " +
        "aria-[invalid=true]:border-error-ink aria-[invalid=true]:focus:ring-error-ink";

    public const string FieldMessage = "text-caption text-error-ink";
}
