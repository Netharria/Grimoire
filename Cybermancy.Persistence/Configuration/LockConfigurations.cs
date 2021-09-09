using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class LockConfigurations : IEntityTypeConfiguration<Lock>
    {
        public void Configure(EntityTypeBuilder<Lock> builder)
        {
            builder.HasKey(e => e.ChannelId);
            builder.HasOne(e => e.Channel).WithOne(e => e.Lock)
                .HasForeignKey<Lock>(x => x.ChannelId)
                .IsRequired();

            builder.HasOne(e => e.Moderator).WithMany(e => e.ChannelsLocked)
                .IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.LockedChannels)
                .IsRequired();
        }
    }
}