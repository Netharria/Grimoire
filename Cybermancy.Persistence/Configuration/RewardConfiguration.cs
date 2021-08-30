using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;
using System.Diagnostics.CodeAnalysis;

namespace Cybermancy.Persistance.Configuration
{
    [ExcludeFromCodeCoverage]
    public class RewardConfiguration : IEntityTypeConfiguration<Reward>
    {
        public void Configure(EntityTypeBuilder<Reward> builder)
        {
            builder.HasKey(e => e.Role);
            builder.HasOne(e => e.Role).WithOne(e => e.Reward)
                .IsRequired();
            builder.HasOne(e => e.Guild).WithMany(e => e.Rewards);
        }
    }
}
