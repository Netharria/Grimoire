using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.Property(e => e.UserName).IsRequired();
            builder.Property(e => e.DisplayName).IsRequired();
            builder.Property(e => e.AvatarUrl).IsRequired();
            builder.HasMany(e => e.Guilds).WithMany(e => e.Users);
            builder.HasMany(e => e.Messages).WithOne(e => e.User);
            builder.HasMany(e => e.Trackers).WithOne(e => e.User).HasForeignKey(e => e.UserId);
            builder.HasMany(e => e.TrackedUsers).WithOne(e => e.Moderator).HasForeignKey(e => e.ModeratorId);
            builder.HasMany(e => e.UserSins).WithOne(e => e.User).HasForeignKey(e => e.UserId);
            builder.HasMany(e => e.ModeratedSins).WithOne(e => e.Moderator).HasForeignKey(e => e.ModeratorId);
            builder.HasMany(e => e.ActiveMutes).WithOne(e => e.User);
            builder.HasMany(e => e.ChannelsLocked).WithOne(e => e.Moderator);
            builder.HasMany(e => e.SinsPardoned).WithOne(e => e.Moderator);
        }
    }
}