// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Microsoft.EntityFrameworkCore;
using Lock = Grimoire.Settings.Domain.Lock;

namespace Grimoire.Settings;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    internal DbSet<GuildSettings> GuildSettings { get; init; }
    internal DbSet<CustomCommandsSettings> GuildCommandsSettings { get; init; }
    internal DbSet<LevelingSettings> GuildLevelSettings { get; init; }
    internal DbSet<UserLogSettings> GuildUserLogSettings { get; init; }
    internal DbSet<MessageLogSettings> GuildMessageLogSettings { get; init; }
    internal DbSet<ModerationSettings> GuildModerationSettings { get; init; }
    internal DbSet<IgnoredChannel> IgnoredChannels { get; init; }
    internal DbSet<IgnoredMember> IgnoredMembers { get; init; }
    internal DbSet<IgnoredRole> IgnoredRoles { get; init; }
    internal DbSet<Lock> Locks { get; init; }
    internal DbSet<MessageLogChannelOverride> MessagesLogChannelOverrides { get; init; }
    internal DbSet<Mute> Mutes { get; init; }
    internal DbSet<Reward> Rewards { get; init; }
    internal DbSet<SpamFilterOverride> SpamFilterOverrides { get; init; }
    internal DbSet<Tracker> Trackers { get; init; }
}
