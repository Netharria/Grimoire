using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class UserLevelConfigurations : IEntityTypeConfiguration<UserLevel>
    {
        public void Configure(EntityTypeBuilder<UserLevel> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new {e.GuildId, e.UserId}).IsUnique();
            builder.HasOne(e => e.Guild).WithMany(e => e.UserLevels)
                .HasForeignKey(e => e.GuildId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.HasOne(e => e.User).WithMany(e => e.UserLevels)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            builder.Property(e => e.IsXpIgnored)
                .HasDefaultValue(false);
        }
    }
}