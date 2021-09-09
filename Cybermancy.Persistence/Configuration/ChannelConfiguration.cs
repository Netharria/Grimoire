using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
    {
        public void Configure(EntityTypeBuilder<Channel> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever()
                .IsRequired();

            builder.Property(e => e.Name)
                .IsRequired();

            builder.HasOne(e => e.Guild).WithMany(e => e.Channels).IsRequired();

            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);

            builder.HasMany(e => e.Messages).WithOne(e => e.Channel);
            builder.HasMany(e => e.Trackers).WithOne(e => e.LogChannel);

            builder.HasOne(e => e.Lock).WithOne(e => e.Channel).IsRequired(false);
        }
    }
}