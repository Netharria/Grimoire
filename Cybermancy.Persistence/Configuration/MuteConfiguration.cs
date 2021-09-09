using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class MuteConfiguration : IEntityTypeConfiguration<Mute>
    {
        public void Configure(EntityTypeBuilder<Mute> builder)
        {
            builder.HasKey(e => e.SinId);
            builder.HasOne(e => e.Sin).WithOne(e => e.Mute)
                .HasForeignKey<Mute>(e => e.SinId)
                .IsRequired();
            builder.HasOne(e => e.User).WithMany(e => e.ActiveMutes);
            builder.HasOne(e => e.Guild).WithMany(e => e.ActiveMutes);
        }
    }
}