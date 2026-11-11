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
    /// <returns>Whether the name was new, or already had a version that this one replaces.</returns>
    private async Task<SaveOutcome> SaveCommandAsync(CustomCommand command)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existed = await dbContext.CustomCommands
            .AnyAsync(x => x.GuildId == command.GuildId && x.Name == command.Name);
        await dbContext.AddAsync(command);
        await dbContext.SaveChangesAsync();
        return existed ? SaveOutcome.Updated : SaveOutcome.Added;
    }

    private enum SaveOutcome
    {
        Added,
        Updated
    }
}
