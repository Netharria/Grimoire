using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class PublishedMessageConfiguration : IEntityTypeConfiguration<PublishedMessage>
    {
        public void Configure(EntityTypeBuilder<PublishedMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.Sin, e.PublishType })
                .IsUnique(true);
            builder.HasOne(e => e.Sin).WithMany(e => e.PublishMessages);
        }
    }
}
