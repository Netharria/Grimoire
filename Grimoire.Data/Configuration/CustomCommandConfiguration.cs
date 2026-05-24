// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class CustomCommandConfiguration : IEntityTypeConfiguration<CustomCommand>,
    IEntityTypeConfiguration<EmbedCustomCommand>
{
    public void Configure(EntityTypeBuilder<CustomCommand> builder)
    {
        builder.HasKey(e => new { e.Name, e.GuildId, e.CreatedAt });

        builder.Property(e => e.Name)
            .HasConversion(name => name.Value, value => CustomCommandName.ParseFromDatabase(value))
            .HasMaxLength(24);

        builder.Property(e => e.Content)
            .HasMaxLength(2000)
            .HasConversion(
                content => content.Value,
                value => CustomCommandContent.ParseFromDatabase(value))
            .IsRequired();

        builder.Property(e => e.GuildId)
            .HasConversion(guildId => guildId.Value, id => new GuildId(id));

        builder.Property(e => e.ModeratorId)
            .HasConversion(e => e.GetValueOrDefault().Value, value => new ModeratorId(value));

        builder.Property(e => e.RolePrecedence)
            .HasConversion<string>()
            .HasDefaultValue(RolePrecedence.DenyOverride)
            .HasColumnName("RolePrecedence");

        builder.HasIndex(e => new { e.GuildId, e.Name });

        builder.HasDiscriminator<string>("CommandType")
            .HasValue<TextCustomCommand>("Text")
            .HasValue<EmbedCustomCommand>("Embed")
            .IsComplete();
    }

    public void Configure(EntityTypeBuilder<EmbedCustomCommand> builder)
    {
        builder.Property(e => e.EmbedColor)
            .HasMaxLength(6)
            .HasConversion(
                color => color.GetValueOrDefault().Value,
                color => CustomCommandEmbedColor.ParseFromDatabase(color))
            .IsRequired(false);
    }
}
