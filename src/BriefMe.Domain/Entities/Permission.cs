using BriefMe.Domain.Common;
using BriefMe.Domain.Exceptions;

namespace BriefMe.Domain.Entities;

public class Permission : Entity, IAuditableEntity
{
    private Permission(Guid id, string code, string name, string? description, string group) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        Group = group;
    }

    private Permission()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Group { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public static Permission Create(string code, string name, string? description, string group)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Código da permission é obrigatório.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da permission é obrigatório.");

        return new Permission(Guid.NewGuid(), code.Trim(), name.Trim(), description, group.Trim());
    }
}
