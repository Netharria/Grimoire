using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
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
