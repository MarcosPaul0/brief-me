namespace BriefMe.Web.Ui;

/// <summary>
/// The button look, shared by <see cref="Button"/> and <see cref="FileButton"/> (a <c>&lt;label&gt;</c> that
/// opens a file picker) so both render identically.
/// </summary>
internal static class ButtonClasses
{
    public static string For(ButtonVariant variant, ButtonSize size, string? extra = null) => ClassNames.Join(
        "inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-md transition-colors " +
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink " +
        "disabled:cursor-not-allowed disabled:opacity-50",
        VariantClasses(variant),
        SizeClasses(variant, size),
        extra);

    private static string VariantClasses(ButtonVariant variant) => variant switch
    {
        ButtonVariant.Secondary => "bg-surface-card text-ink hover:bg-surface-strong",
        ButtonVariant.Outline => "border border-hairline bg-canvas text-ink hover:bg-surface-soft",
        ButtonVariant.Ghost => "text-body hover:bg-surface-card hover:text-ink",
        ButtonVariant.Link => "font-semibold text-ink underline underline-offset-4",
        ButtonVariant.Destructive => "bg-error-ink text-on-primary active:bg-error-ink/90",
        _ => "bg-primary text-on-primary active:bg-primary-active " +
             "disabled:bg-primary-disabled disabled:text-muted disabled:opacity-100"
    };

    private static string SizeClasses(ButtonVariant variant, ButtonSize size) => (variant, size) switch
    {
        // A link-styled button keeps the text's own box so it lines up with surrounding copy.
        (ButtonVariant.Link, ButtonSize.Sm) => "gap-1.5 text-body-sm",
        (ButtonVariant.Link, _) => "gap-1.5 text-body-md",
        (_, ButtonSize.Sm) => "h-9 rounded-sm px-3.5 text-button",
        _ => "h-11 px-5 text-button"
    };
}
