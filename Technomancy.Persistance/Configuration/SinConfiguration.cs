using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Technomancy.Domain;

namespace Technomancy.Persistance.Configuration
{
    public class SinConfiguration : IEntityTypeConfiguration<Sin>
    {
        public void Configure(EntityTypeBuilder<Sin> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();
            builder.HasOne(e => e.User).WithMany(e => e.UserSins).HasForeignKey(e => e.UserId);
            builder.HasOne(e => e.Moderator).WithMany(e => e.ModeratedSins).HasForeignKey(e => e.ModeratorId);
            builder.HasOne(e => e.Guild).WithMany(e => e.Sins);
            builder.HasOne(e => e.Mute).WithOne(e => e.Sin).IsRequired(false);
            builder.HasOne(e => e.Pardon).WithOne(e => e.Sin).IsRequired(false);
            builder.HasMany(e => e.PublishMessages).WithOne(e => e.Sin);
        }
    }
}
