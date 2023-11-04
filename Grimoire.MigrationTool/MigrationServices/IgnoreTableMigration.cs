// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.MigrationTool.Domain;

namespace Grimoire.MigrationTool.MigrationServices;
internal class IgnoreTableMigration
{
    public static async Task MigrateIgnoreEntriesAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();

#pragma warning disable CS0618 // Type or member is obsolete
        var channels = grimoireDbContext.Channels.Where(x => x.IsXpIgnored)
            .Select(x => new IgnoredChannel
            {
                ChannelId = x.Id,
                GuildId = x.GuildId,
            }).ToList().DistinctBy(x => new { x.ChannelId, x.GuildId });
        await grimoireDbContext.IgnoredChannels.AddRangeAsync(channels);

        var members = grimoireDbContext.Members.Where(x => x.IsXpIgnored)
            .Select(x => new IgnoredMember
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
            }).ToList().DistinctBy(x => new { x.UserId, x.GuildId });
        await grimoireDbContext.IgnoredMembers.AddRangeAsync(members);

        var roles = grimoireDbContext.Roles.Where(x => x.IsXpIgnored)
            .Select(x => new IgnoredRole
            {
                RoleId = x.Id,
                GuildId = x.GuildId,
            }).ToList().DistinctBy(x => new { x.RoleId, x.GuildId });
        await grimoireDbContext.IgnoredRoles.AddRangeAsync(roles);
#pragma warning restore CS0618 // Type or member is obsolete

        await grimoireDbContext.SaveChangesAsync();
    }
}
