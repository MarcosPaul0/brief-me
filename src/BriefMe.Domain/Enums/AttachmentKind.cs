namespace BriefMe.Domain.Enums;

/// <summary>
/// The family an attachment belongs to, derived from its detected format. Drives which preview the UI renders.
/// </summary>
public enum AttachmentKind
{
    Pdf = 1,
    Image = 2,
    Audio = 3,
    Video = 4
}
