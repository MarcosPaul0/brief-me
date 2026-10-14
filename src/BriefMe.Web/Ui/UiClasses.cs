namespace BriefMe.Web.Ui;

/// <summary>
/// Class strings shared by more than one primitive, so the plain fields (<see cref="Input"/>, ...) and the
/// EditForm-bound ones (<see cref="FormInput"/>, ...) render identically.
/// </summary>
internal static class UiClasses
{
    /// <summary>A text-like control on the canvas: hairline rule, 8px corner, primary focus ring.</summary>
    public const string Control =
        "w-full rounded-md border border-hairline/40 bg-canvas px-3 py-2.5 text-list text-ink-body " +
        "placeholder:text-ink-meta/70 transition-colors focus:border-primary focus:outline-none " +
        "focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-chrome disabled:opacity-70 " +
        "aria-[invalid=true]:border-negative";

    public const string FieldMessage = "text-caption text-negative";
}
