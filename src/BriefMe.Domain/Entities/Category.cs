using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

/// <summary>
/// The kind of work a briefing asks for. Managed by the admin; requesters pick one of the active ones when
/// opening a briefing. Deactivating hides a category from new briefings without touching the ones that
/// already use it, so categories are never deleted.
/// </summary>
public class Category : AggregateRoot, IAuditableEntity
{
    public const int NameMaxLength = 100;

    public const int DescriptionMaxLength = 500;

    private Category(Guid id, string name, string? description) : base(id)
    {
        Name = name;
        Description = description;
        IsActive = true;
    }

    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public static Category Create(string name, string? description = null) =>
        new(Guid.NewGuid(), NormalizeName(name), NormalizeDescription(description));

    public void Rename(string name) => Name = NormalizeName(name);

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Category name must be at most {NameMaxLength} characters.");

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var trimmed = description.Trim();
        if (trimmed.Length > DescriptionMaxLength)
            throw new DomainException($"Category description must be at most {DescriptionMaxLength} characters.");

        return trimmed;
    }
}
