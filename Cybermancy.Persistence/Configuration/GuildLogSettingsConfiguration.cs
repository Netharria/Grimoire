using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
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
