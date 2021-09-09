using System.Diagnostics.CodeAnalysis;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cybermancy.Persistence.Configuration
{
    [ExcludeFromCodeCoverage]
    public class RewardConfiguration : IEntityTypeConfiguration<Reward>
    {
        public void Configure(EntityTypeBuilder<Reward> builder)
        {
            builder.HasKey(e => e.RoleId);
            builder.HasOne(e => e.Role).WithOne(e => e.Reward)
                .HasForeignKey<Reward>(e => e.RoleId)
                .IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.Rewards);
        }
    }
}