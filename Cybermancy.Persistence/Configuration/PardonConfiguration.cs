using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    public class PardonConfiguration : IEntityTypeConfiguration<Pardon>
    {
        public void Configure(EntityTypeBuilder<Pardon> builder)
        {
            builder.HasKey(e => e.Sin);
            builder.HasOne(e => e.Sin).WithOne(e => e.Pardon)
                .IsRequired();
            builder.HasOne(e => e.Moderator).WithMany(e => e.SinsPardoned);
        }
    }
}
