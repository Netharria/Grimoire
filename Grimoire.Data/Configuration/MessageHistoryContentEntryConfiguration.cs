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
internal sealed class MessageHistoryContentEntryConfiguration : IEntityTypeConfiguration<MessageHistoryContentEntry>
{
    public void Configure(EntityTypeBuilder<MessageHistoryContentEntry> builder)
    {
        builder.Property(e => e.Content)
            .HasColumnName("Content")
            .HasMaxLength(4000)
            .HasConversion(c => c.Content, v => MessageContent.FromDatabase(v));
    }
}
