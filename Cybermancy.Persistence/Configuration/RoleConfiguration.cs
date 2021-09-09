using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.Roles);
            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);
            builder.HasOne(e => e.Reward).WithOne(e => e.Role)
                .IsRequired(false);
        }
    }
}