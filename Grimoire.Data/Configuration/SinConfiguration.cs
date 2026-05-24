// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class SinConfiguration : IEntityTypeConfiguration<Sin>
{
    public void Configure(EntityTypeBuilder<Sin> builder)
    {
        builder.HasKey(sin => sin.Id);
        builder.Property(sin => sin.Id)
            .UseIdentityAlwaysColumn()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(sin => sin.SinOn)
            .HasDefaultValueSql("now()");

        // Primary query pattern: Filter by UserId + GuildId, order by SinOn
        builder.HasIndex(sin => new { sin.UserId, sin.GuildId, sin.SinOn })
            .HasDatabaseName("IX_Sin_UserId_GuildId_SinOn");

        // For moderator stats: GroupBy SinType after filtering Actor + GuildId
        builder.HasIndex(sin => new { sin.Actor, sin.GuildId, sin.SinType })
            .HasDatabaseName("IX_Sin_ModeratorId_GuildId_SinType");

        // For single sin lookups: Filter by Id + GuildId
        builder.HasIndex(sin => new { sin.Id, sin.GuildId })
            .HasDatabaseName("IX_Sin_Id_GuildId");

        var actorConverter = new ValueConverter<ModerationActor, ulong?>(
            actor => actor as ModerationActor.Moderator != null
                ? ((ModerationActor.Moderator)actor).Id.Value
                : null,
            value => value.HasValue
                ? new ModerationActor.Moderator(new ModeratorId(value.Value))
                : new ModerationActor.System());

        builder.Property(e => e.Actor)
            .HasConversion(actorConverter)
            .HasColumnName("ModeratorId")
            .IsRequired(false);

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));

        builder.Property(e => e.Id)
            .HasConversion(e => e.Value, value => new SinId(value));
        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));
    }
}
