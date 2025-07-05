// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.DatabaseQueryHelpers;

public static class GrimoireDbContextQueryHelpers
{
    public static async Task AddMissingMember(this GrimoireDbContext dbContext, ulong userId, ulong guildId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.Id == userId, cancellationToken))
            await dbContext.Users.AddAsync(new User { Id = userId }, cancellationToken);
        await dbContext.Members.AddAsync(new Member
        {
            UserId = userId,
            GuildId = guildId,
            XpHistory =
            [
                new XpHistory
                {
                    UserId = userId,
                    GuildId = guildId,
                    Xp = 0,
                    Type = XpHistoryType.Created,
                    TimeOut = DateTime.UtcNow
                }
            ]
        }, cancellationToken);
    }
}
