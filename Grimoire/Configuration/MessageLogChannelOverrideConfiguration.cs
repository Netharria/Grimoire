// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class MessageLogChannelOverrideConfiguration : IEntityTypeConfiguration<MessageLogChannelOverride>
{
    public void Configure(EntityTypeBuilder<MessageLogChannelOverride> builder)
    {
        builder.HasKey(x => x.ChannelId);
        builder.HasOne(x => x.Channel)
            .WithOne(x => x.MessageLogChannelOverride)
            .HasForeignKey<MessageLogChannelOverride>(x => x.ChannelId)
            .IsRequired();
        builder.HasOne(x => x.Guild)
            .WithMany(x => x.MessageLogChannelOverrides)
            .HasForeignKey(x => x.GuildId)
            .IsRequired();
        builder.Property(x => x.ChannelId)
            .ValueGeneratedNever();
        builder.Property(x => x.ChannelOption)
            .IsRequired();
    }
}
