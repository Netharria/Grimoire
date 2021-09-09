using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class PardonConfiguration : IEntityTypeConfiguration<Pardon>
    {
        public void Configure(EntityTypeBuilder<Pardon> builder)
        {
            builder.HasKey(e => e.SinId);
            builder.HasOne(e => e.Sin).WithOne(e => e.Pardon)
                .HasForeignKey<Pardon>(e => e.SinId)
                .IsRequired();
            builder.HasOne(e => e.Moderator).WithMany(e => e.SinsPardoned);
        }
    }
}