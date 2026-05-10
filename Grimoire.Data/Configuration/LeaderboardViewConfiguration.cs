// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

public class LeaderboardViewConfiguration : IEntityTypeConfiguration<LeaderboardView>
{
    public void Configure(EntityTypeBuilder<LeaderboardView> builder)
    {
        builder.ToView("leaderboard_view")
            .HasNoKey();

        builder.Property(e => e.UserId)
            .HasConversion(e => e.Value, value => new UserId(value));

        builder.Property(e => e.GuildId)
            .HasConversion(e => e.Value, value => new GuildId(value));
    }
}
