using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    class GuildModerationSettingsConfiguration : IEntityTypeConfiguration<GuildModerationSettings>
    {
        public void Configure(EntityTypeBuilder<GuildModerationSettings> builder)
        {
            builder.HasKey(e => e.Guild);
            builder.HasOne(e => e.Guild).WithOne(e => e.ModerationSettings)
                .IsRequired();

            builder.Property(e => e.DurationType)
                .HasDefaultValue(Duration.Years);
            builder.Property(e => e.Duration)
                .HasDefaultValue(30);
        }
    }
}
