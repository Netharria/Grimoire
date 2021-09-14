using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    internal class GuildModerationSettingsConfiguration : IEntityTypeConfiguration<GuildModerationSettings>
    {
        public void Configure(EntityTypeBuilder<GuildModerationSettings> builder)
        {
            builder.HasKey(e => e.GuildId);
            builder.HasOne(e => e.Guild).WithOne(e => e.ModerationSettings)
                .HasForeignKey<GuildModerationSettings>(x => x.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(e => e.DurationType)
                .HasDefaultValue(Duration.Years);
            builder.Property(e => e.Duration)
                .HasDefaultValue(30);
        }
    }
}