// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.CustomCommands;

[Command("Commands")]
[RequireGuild]
[RequireModuleEnabled(Module.Commands)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public sealed partial class CustomCommandSettings(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
}
