using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class UserLevelConfigurations : IEntityTypeConfiguration<UserLevels>
    {
        public void Configure(EntityTypeBuilder<UserLevels> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever().IsRequired();
            builder.HasIndex(e => new {e.GuildId, e.UserId}).IsUnique();
            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);
        }
    }
}