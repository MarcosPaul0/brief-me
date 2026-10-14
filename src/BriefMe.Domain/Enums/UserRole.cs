namespace BriefMe.Domain.Enums;

/// <summary>
/// The three fixed profiles. Each user holds exactly one; it travels in the access token as the role claim
/// (its member name), which is what controllers and pages authorize on.
/// </summary>
public enum UserRole
{
    /// <summary>Manages users and categories. Takes no part in the briefing flow.</summary>
    Admin = 1,

    /// <summary>Claims briefings from the open queue and delivers them.</summary>
    Designer = 2,

    /// <summary>Writes briefings, sends them to the queue and accepts or sends back the delivery.</summary>
    Requester = 3
}
