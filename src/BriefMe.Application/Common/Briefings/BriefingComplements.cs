namespace BriefMe.Application.Common.Briefings;

/// <summary>
/// The display names a briefing read model needs next to its ids, already loaded. A missing id yields
/// <c>null</c> rather than throwing, so one unknown user degrades a field instead of the whole list.
/// </summary>
public sealed class BriefingComplements(
    IReadOnlyDictionary<Guid, string> categoryNames,
    IReadOnlyDictionary<Guid, string> userNames)
{
    public string? CategoryName(Guid categoryId) => categoryNames.GetValueOrDefault(categoryId);

    public string? UserName(Guid? userId) => userId is { } id ? userNames.GetValueOrDefault(id) : null;
}
