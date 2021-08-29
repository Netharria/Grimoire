using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class UserLevelConfigurations : IEntityTypeConfiguration<UserLevels>
    {
        public void Configure(EntityTypeBuilder<UserLevels> builder)
        {
            builder.HasIndex(e => new { e.Guild, e.User }).IsUnique();
            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);
        }
    }
}
