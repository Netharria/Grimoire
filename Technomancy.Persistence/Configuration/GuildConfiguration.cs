using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class GuildConfiguration : IEntityTypeConfiguration<Guild>
    {
        public void Configure(EntityTypeBuilder<Guild> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasMany(e => e.Channels).WithOne(e => e.Guild);
            builder.HasMany(e => e.Roles).WithOne(e => e.Guild);
            builder.HasMany(e => e.Messages).WithOne(e => e.Guild);
            builder.HasMany(e => e.Users).WithMany(e => e.Guilds);
            builder.HasMany(e => e.OldLogMessages).WithOne(e => e.Guild);
            builder.HasMany(e => e.Trackers).WithOne(e => e.Guild);
            builder.HasMany(e => e.Rewards).WithOne(e => e.Guild);
            builder.HasMany(e => e.UserLevels).WithOne(e => e.Guild);
            builder.HasMany(e => e.Sins).WithOne(e => e.Guild);
            builder.HasMany(e => e.ActiveMutes).WithOne(e => e.Guild);
            builder.HasMany(e => e.LockedChannels).WithOne(e => e.Guild);

            builder.HasOne(e => e.LogSettings).WithOne(e => e.Guild)
                .IsRequired(false);
            builder.HasOne(e => e.LevelSettings).WithOne(e => e.Guild)
                .IsRequired(false);
            builder.HasOne(e => e.ModerationSettings).WithOne(e => e.Guild)
                .IsRequired(false);
        }
    }
}
