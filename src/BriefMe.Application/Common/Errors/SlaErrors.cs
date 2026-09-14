namespace BriefMe.Application.Common.Errors;

/// <summary>
/// Failure messages of the SLA use cases (RF027, BRF-042).
/// </summary>
/// <remarks>
/// The deadline range is deliberately absent: <c>SlaDeadline.Create</c> owns what a valid deadline is and
/// returns its own message, so restating <c>[1, 8760]</c> here would give the same rule two homes
/// (<c>.claude/rules/error-messages.md</c>, "Out of scope"). The category message is reused from
/// <see cref="CategoryErrors"/> for the same reason — the owning concept wins over the calling folder.
/// </remarks>
public static class SlaErrors
{
    public static class Validation
    {
        /// <summary>
        /// A priority outside the enum. Absent priority is valid — it is what makes a rule apply to any
        /// priority — so this is about a filled-in value that names nothing.
        /// </summary>
        public const string InvalidPriority = "Invalid SLA rule priority.";
    }
}
