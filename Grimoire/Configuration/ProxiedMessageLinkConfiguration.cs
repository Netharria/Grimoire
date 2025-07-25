// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class ProxiedMessageLinkConfiguration : IEntityTypeConfiguration<ProxiedMessageLink>
{
    public void Configure(EntityTypeBuilder<ProxiedMessageLink> builder)
    {
        builder.HasKey(x => new { x.ProxyMessageId, x.OriginalMessageId });
        builder.HasOne(x => x.ProxyMessage)
            .WithOne(x => x.ProxiedMessageLink)
            .HasForeignKey<ProxiedMessageLink>(x => x.ProxyMessageId);
        builder.HasOne(x => x.OriginalMessage)
            .WithOne(x => x.OriginalMessageLink)
            .HasForeignKey<ProxiedMessageLink>(x => x.OriginalMessageId);
        builder.Property(x => x.ProxyMessageId)
            .IsRequired();
        builder.Property(x => x.OriginalMessageId)
            .IsRequired();
        builder.Property(x => x.SystemId)
            .IsRequired(false)
            .HasMaxLength(256);
        builder.Property(x => x.MemberId)
            .IsRequired(false)
            .HasMaxLength(256);
    }
}
