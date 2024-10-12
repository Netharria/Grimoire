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
internal sealed class MemberConfigurations : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(e => new { e.UserId, e.GuildId });
        builder.HasOne(e => e.Guild)
            .WithMany(e => e.Members)
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(e => e.User)
            .WithMany(e => e.MemberProfiles)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
