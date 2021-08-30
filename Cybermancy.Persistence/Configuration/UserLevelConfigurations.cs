using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
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
