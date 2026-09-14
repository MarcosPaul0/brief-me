namespace BriefMe.Application.Common.Dtos;

public sealed record PermissionGroupDto(
    string Group,
    IReadOnlyCollection<PermissionItemDto> Permissions);
