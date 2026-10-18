namespace BriefMe.Web.Ui;

/// <summary>
/// What a tag means, not which feature it belongs to — features map their own states onto these
/// (e.g. <c>BriefingLabels.StatusBadgeVariant</c>).
/// </summary>
public enum BadgeVariant
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}
