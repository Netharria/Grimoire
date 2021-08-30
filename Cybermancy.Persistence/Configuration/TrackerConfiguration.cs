using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cybermancy.Domain;

namespace Cybermancy.Persistance.Configuration
{
    public class TrackerConfiguration : IEntityTypeConfiguration<Tracker>
    {
        public void Configure(EntityTypeBuilder<Tracker> builder)
        {
            builder.HasKey(e => e.User);
            builder.HasOne(e => e.User).WithMany(e => e.Trackers).HasForeignKey(e => e.UserId);
            builder.HasOne(e => e.Guild).WithMany(e => e.Trackers);
            builder.HasOne(e => e.LogChannel).WithMany(e => e.Trackers);
            builder.HasOne(e => e.Moderator).WithMany(e => e.TrackedUsers).HasForeignKey(e => e.ModeratorId);
        }
    }
}
