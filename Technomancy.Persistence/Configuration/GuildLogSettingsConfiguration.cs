using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class GuildLogSettingsConfiguration : IEntityTypeConfiguration<GuildLogSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLogSettings> builder)
        {
            builder.HasKey(e => e.Guild);
            builder.HasOne(e => e.Guild).WithOne(e => e.LogSettings)
                .IsRequired();
        }
    }
}
