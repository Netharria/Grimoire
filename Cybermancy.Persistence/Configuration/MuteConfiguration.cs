using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    public class MuteConfiguration : IEntityTypeConfiguration<Mute>
    {
        public void Configure(EntityTypeBuilder<Mute> builder)
        {
            builder.HasKey(e => e.Sin);
            builder.HasOne(e => e.Sin).WithOne(e => e.Mute)
                .IsRequired();
            builder.HasOne(e => e.User).WithMany(e => e.ActiveMutes);
            builder.HasOne(e => e.Guild).WithMany(e => e.ActiveMutes);
        }
    }
}
