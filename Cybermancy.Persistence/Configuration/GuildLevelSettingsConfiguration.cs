using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class GuildLevelSettingsConfiguration : IEntityTypeConfiguration<GuildLevelSettings>
    {
        public void Configure(EntityTypeBuilder<GuildLevelSettings> builder)
        {
            builder.HasKey(x => x.GuildId);
            builder.HasOne(e => e.Guild).WithOne(e => e.LevelSettings)
                .HasForeignKey<GuildLevelSettings>(x => x.GuildId)
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