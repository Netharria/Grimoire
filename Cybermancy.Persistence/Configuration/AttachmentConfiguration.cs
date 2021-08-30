using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.HasKey(e => e.Message);
            builder.Property(e => e.Message)
                .IsRequired();

            builder.Property(e => e.AttachmentUrl)
                .IsRequired();
        }
    }
}
