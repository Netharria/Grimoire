// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.SharedDtos;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class GuildUserDatabaseQueryHelpers
    {
        public static async Task<bool> AddMissingGuildUsersAsync(this DbSet<GuildUser> databaseGuildUsers, IEnumerable<GuildUserDto> guildUsers, CancellationToken cancellationToken)
        {
            var guildUsersToAdd = guildUsers
                .ExceptBy(databaseGuildUsers.Select(x => new { x.UserId, x.GuildId }),
                x => new { x.UserId, x.GuildId })
                .Select(x =>
                {
                    var guildUser = new GuildUser
                    {
                        UserId = x.UserId,
                        GuildId = x.GuildId
                    };
                    if(x.Nickname != null)
                        guildUser.NicknamesHistory.Add(
                        new NicknameHistory
                        {
                            GuildId = x.GuildId,
                            UserId = x.UserId,
                            Nickname = x.Nickname
                        });
                    return guildUser;
                });

            if (guildUsersToAdd.Any())
                await databaseGuildUsers.AddRangeAsync(guildUsersToAdd, cancellationToken);
            return guildUsersToAdd.Any();
        }
    }
}
