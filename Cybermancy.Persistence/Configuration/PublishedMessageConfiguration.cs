using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class PublishedMessageConfiguration : IEntityTypeConfiguration<PublishedMessage>
    {
        public void Configure(EntityTypeBuilder<PublishedMessage> builder)
        {
            builder.HasKey(e => e.MessageId);
            builder.Property(e => e.MessageId).ValueGeneratedNever().IsRequired();
            builder.HasIndex(e => new {e.SinId, e.PublishType})
                .IsUnique();
            builder.HasOne(e => e.Sin).WithMany(e => e.PublishMessages)
                .HasForeignKey(e => e.SinId);
        }
    }
}