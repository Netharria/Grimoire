// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Obsolete;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Grimoire.Configuration;

internal sealed class IgnoredMemberConfiguration : IEntityTypeConfiguration<IgnoredMember>
{
    public void Configure(EntityTypeBuilder<IgnoredMember> builder)
    {
        builder.HasKey(e => new { e.UserId, e.GuildId });
    }
}
