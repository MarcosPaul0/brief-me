namespace BriefMe.Domain.Repositories;

public enum UserSortField
{
    FullName,
    Email,
    CreatedAt
}

public sealed record UserSort(UserSortField Field = UserSortField.FullName, bool Descending = false);
