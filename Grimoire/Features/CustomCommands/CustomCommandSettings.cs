// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.CustomCommands;

[Command("Commands")]
public sealed partial class CustomCommandSettings(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    GuildLog guildLog)
{
    /// <summary>Saves a new command, or a new version of one: versions are rows, so nothing is ever updated.</summary>
    private async Task SaveCommandAsync(CustomCommand command)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.AddAsync(command);
        await dbContext.SaveChangesAsync();
    }
}
