using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
    public class OldLogMessageConfiguration : IEntityTypeConfiguration<OldLogMessage>
    {
        public void Configure(EntityTypeBuilder<OldLogMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ChannelId).IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.OldLogMessages);
        }
    }
}
