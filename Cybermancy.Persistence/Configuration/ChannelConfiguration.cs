using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
    {
        public void Configure(EntityTypeBuilder<Channel> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .IsRequired();

            builder.Property(e => e.Name)
                .IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.Channels);
            builder.Property(e => e.Guild)
                .IsRequired();

            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);

            builder.HasMany(e => e.Messages).WithOne(e => e.Channel);
            builder.HasMany(e => e.Trackers).WithOne(e => e.LogChannel);

            builder.HasOne(e => e.Lock).WithOne(e => e.Channel);
            builder.Property(e => e.Lock)
                .IsRequired(false);
        }
    }
}
