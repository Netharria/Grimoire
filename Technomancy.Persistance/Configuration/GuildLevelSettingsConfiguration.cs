using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class GuildLevelSettingsConfiguration : IEntityTypeConfiguration<GuildLevelSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLevelSettings> builder)
        {
            builder.HasKey(e => e.Guild);
            builder.HasOne(e => e.Guild).WithOne(e => e.LevelSettings)
                .IsRequired();
            builder.Property(e => e.TextTime)
                .HasDefaultValue(3);
            builder.Property(e => e.Base)
                .HasDefaultValue(15);
            builder.Property(e => e.Modifier)
                .HasDefaultValue(50);
            builder.Property(e => e.Amount)
                .HasDefaultValue(5);
        }
    }
}
