using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class OldLogMessageConfiguration : IEntityTypeConfiguration<OldLogMessage>
    {
        public void Configure(EntityTypeBuilder<OldLogMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.Property(e => e.ChannelId).IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.OldLogMessages);
        }
    }
}