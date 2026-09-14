using System.Reflection;
using BriefMe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BriefMe.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Briefing> Briefings => Set<Briefing>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<BriefingTask> Tasks => Set<BriefingTask>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<SlaRule> SlaRules => Set<SlaRule>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}
