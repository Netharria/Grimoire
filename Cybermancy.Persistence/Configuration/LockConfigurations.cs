using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    public class LockConfigurations : IEntityTypeConfiguration<Lock>
    {
        public void Configure(EntityTypeBuilder<Lock> builder)
        {
            builder.HasKey(e => e.Channel);
            builder.HasOne(e => e.Channel).WithOne(e => e.Lock)
                .IsRequired();

            builder.HasOne(e => e.Moderator).WithMany(e => e.ChannelsLocked)
                .IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.LockedChannels)
                .IsRequired();
        }
    }
}
