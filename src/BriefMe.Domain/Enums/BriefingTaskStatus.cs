namespace BriefMe.Domain.Enums;

/// <summary>
/// The seven task states of RF020. Named <c>BriefingTaskStatus</c> rather than <c>TaskStatus</c> to avoid
/// colliding with <see cref="System.Threading.Tasks.TaskStatus"/>, which implicit usings put in scope
/// everywhere.
/// </summary>
public enum BriefingTaskStatus
{
    NotStarted = 1,
    InProgress = 2,
    AwaitingTechnicalApproval = 3,
    AwaitingRequesterApproval = 4,
    Approved = 5,
    Rejected = 6,
    Blocked = 7
}
