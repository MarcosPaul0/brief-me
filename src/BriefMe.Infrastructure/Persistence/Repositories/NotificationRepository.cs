using BriefMe.Domain.Entities;
using BriefMe.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence.Repositories;

public class NotificationRepository(AppDbContext dbContext)
    : EfRepository<Notification>(dbContext), INotificationRepository
{
    public async Task AddRangeAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken = default) =>
        await DbContext.Notifications.AddRangeAsync(notifications, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetByRecipientAsync(
        Guid recipientId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        // Newest first, with Id breaking ties on CreatedAt: an RF034 batch stamps its three rows within
        // ticks of each other, and without the tiebreaker the pages of one listing could overlap or skip.
        await ByRecipient(recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        await ByRecipient(recipientId).CountAsync(cancellationToken);

    public async Task<int> CountUnreadByRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        await Unread(recipientId).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadByRecipientAsync(
        Guid recipientId,
        CancellationToken cancellationToken = default) =>
        await Unread(recipientId).ToListAsync(cancellationToken);

    private IQueryable<Notification> ByRecipient(Guid recipientId) =>
        DbContext.Notifications.Where(n => n.RecipientId == recipientId);

    /// <summary>
    /// Never read: <c>ReadAt</c> is the mark, and <c>IsRead</c> is derived and unmapped (BRF-048), so the
    /// predicate has to be written against the column the index covers.
    /// </summary>
    private IQueryable<Notification> Unread(Guid recipientId) =>
        ByRecipient(recipientId).Where(n => n.ReadAt == null);
}
