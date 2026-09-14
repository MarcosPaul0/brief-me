namespace BriefMe.Application.Common.Dtos;

/// <summary>
/// Output representation of an active briefing category for the create form's picker (BRF-013, RF040).
/// Kept minimal (id + display name) — the full category read model arrives with BRF-059.
/// </summary>
public sealed record CategoryListItemDto(Guid Id, string Name);
