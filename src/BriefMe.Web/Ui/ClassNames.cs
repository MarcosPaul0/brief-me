namespace BriefMe.Web.Ui;

/// <summary>
/// Joins CSS class fragments, skipping null and blank ones — the <c>cn()</c> of shadcn/ui. A primitive puts its
/// own classes first and the caller's <c>Class</c> last, so the caller can extend (not replace) the base look.
/// Unlike shadcn there is no tailwind-merge: when a caller class targets a property the primitive already sets
/// (e.g. <c>text-*</c> on a link button), the CSS source order decides — mark the override important
/// (<c>text-error-ink!</c>) instead of relying on attribute order.
/// </summary>
public static class ClassNames
{
    public static string Join(params string?[] classes) =>
        string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()));
}
