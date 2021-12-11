using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Contracts.Persistance
{
    public interface ICybermancyDbContext
    {
        public DbSet<Attachment> Attachments { get; set; }

        public DbSet<Channel> Channels { get; set; }

        public DbSet<Guild> Guilds { get; set; }

        public DbSet<GuildLevelSettings> GuildLevelSettings { get; set; }

        public DbSet<GuildLogSettings> GuildLogSettings { get; set; }

        public DbSet<GuildModerationSettings> GuildModerationSettings { get; set; }

        public DbSet<GuildUser> GuildUsers { get; set; }

        public DbSet<Lock> Locks { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<Mute> Mutes { get; set; }

        public DbSet<OldLogMessage> OldLogMessages { get; set; }

        public DbSet<Pardon> Pardons { get; set; }

        public DbSet<PublishedMessage> PublishedMessages { get; set; }

        public DbSet<Reward> Rewards { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<Sin> Sins { get; set; }

        public DbSet<Tracker> Trackers { get; set; }

        public DbSet<User> Users { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);
    }
}
