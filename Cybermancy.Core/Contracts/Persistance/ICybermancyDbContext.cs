using System.Linq.Expressions;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cybermancy.Core.Contracts.Persistance
{
    public interface ICybermancyDbContext
    {
        public DbSet<Attachment> Attachments { get; }

        public DbSet<Channel> Channels { get; }

        public DbSet<Guild> Guilds { get; }

        public DbSet<GuildLevelSettings> GuildLevelSettings { get; }

        public DbSet<GuildLogSettings> GuildLogSettings { get; }

        public DbSet<GuildModerationSettings> GuildModerationSettings { get; }

        public DbSet<Member> Members { get; }

        public DbSet<Lock> Locks { get; }

        public DbSet<Message> Messages { get; }

        public DbSet<MessageHistory> MessageHistory { get; }

        public DbSet<Mute> Mutes { get; }

        public DbSet<NicknameHistory> NicknameHistory { get; }

        public DbSet<OldLogMessage> OldLogMessages { get; }

        public DbSet<Pardon> Pardons { get; }

        public DbSet<PublishedMessage> PublishedMessages { get; }

        public DbSet<Reward> Rewards { get; }

        public DbSet<Role> Roles { get; }

        public DbSet<Sin> Sins { get; }

        public DbSet<Tracker> Trackers { get; }

        public DbSet<User> Users { get; }

        public DbSet<UsernameHistory> UsernameHistory { get; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

        public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    }
}
