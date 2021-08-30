using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.User).WithMany(e => e.Messages);
            builder.HasOne(e => e.Channel).WithMany(e => e.Messages);
            builder.HasOne(e => e.Guild).WithMany(e => e.Messages);

            builder.HasMany(e => e.Attachments).WithOne(e => e.Message);
        }
    }
}
