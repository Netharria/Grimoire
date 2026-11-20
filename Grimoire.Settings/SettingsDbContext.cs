// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;

using Grimoire.Data.Conventions;
namespace Grimoire.Settings;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    internal DbSet<GuildSettingRow> GuildSettings { get; init; }
    internal DbSet<ChannelLockRow> ChannelLocks { get; init; }
    internal DbSet<ThreadLockRow> ThreadLocks { get; init; }
    internal DbSet<MuteRow> Mutes { get; init; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureStronglyTypedIds();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SettingsModel.Schema);
        SettingsModel.Configure(modelBuilder);
    }
}
