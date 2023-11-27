// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EFCore.BulkExtensions;
using Grimoire.Domain;
using Grimoire.MigrationTool.Domain;
using Grimoire.MigrationTool.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.MigrationTool.MigrationServices;

public class AnubisMigrationService(AnubisDbContext anubisContext)
{
    private readonly AnubisDbContext _anubisContext = anubisContext;

    public async Task MigrateAnubisDatabaseAsync()
    {
        await this.MigrateSettingsAsync();
        await this.MigrateUsersAsync();
        await this.MigrateMembersAsync();
        await this.MigrateUserXpAsync();
        await this.MigrateRolesAsync();
        await this.MigrateRewardsAsync();
        await this.MigrateChannelsAsync();
        await this.MigrateIgnoredUsersAsync();
        await this.MigrateIgnoredRolesAsync();
        await this.MigrateIgnoredChannelsAsync();
    }

    private async Task MigrateIgnoredChannelsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var ignoredAnubisChannels = await this._anubisContext.IgnoredChannels
            .Select(x => new IgnoredChannel{ ChannelId = x.ChannelId, GuildId = x.GuildId })
            .ToListAsync();

        foreach (var ignoredChannel in ignoredAnubisChannels)
        {

            var grimoireChannel = await grimoireDbContext.IgnoredChannels
                .AnyAsync(x => x.ChannelId == ignoredChannel.ChannelId);
            if (!grimoireChannel)
            {
                await grimoireDbContext.IgnoredChannels.AddAsync(ignoredChannel);
            }
        }
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateIgnoredRolesAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var ignoredAnubisRoles = await this._anubisContext.IgnoredRoles
            .Select(x => new IgnoredRole { RoleId = x.RoleId, GuildId = x.GuildId })
            .ToListAsync();

        foreach (var ignoredRole in ignoredAnubisRoles)
        {

            var grimoireChannel = await grimoireDbContext.IgnoredRoles
                .AnyAsync(x => x.RoleId == ignoredRole.RoleId);
            if (!grimoireChannel)
            {
                await grimoireDbContext.IgnoredRoles.AddAsync(ignoredRole);
            }
        }
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateIgnoredUsersAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var ignoredAnubisUsers = await this._anubisContext.UserLevels
            .Where(x => x.IgnoredXp)
            .Select(x => new IgnoredMember { UserId = x.UserId, GuildId = x.GuildId })
            .ToListAsync();

        foreach (var ignoredMembers in ignoredAnubisUsers)
        {

            var grimoireChannel = await grimoireDbContext.IgnoredMembers
                .AnyAsync(x => x.UserId == ignoredMembers.UserId);
            if (!grimoireChannel)
            {
                await grimoireDbContext.IgnoredMembers.AddAsync(ignoredMembers);
            }
        }
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateChannelsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisChannels = await this._anubisContext.IgnoredChannels.Select(x => new Channel
        {
            GuildId = x.GuildId,
            Id = x.ChannelId
        }).ToListAsync();

        var grimoireChannels = await grimoireDbContext.Channels.Select(x => x.Id).ToListAsync();

        var channelsToAdd = anubisChannels.ExceptBy(grimoireChannels, x => x.Id).ToList();

        await grimoireDbContext.BulkInsertAsync(channelsToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateRewardsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisRewards = await this._anubisContext.Rewards.Select(x => new Reward
        {
            GuildId = x.GuildId,
            RoleId = x.RewardRole,
            RewardLevel = x.RewardLevel
        }).ToListAsync();

        var grimoireRewards = await grimoireDbContext.Rewards.Select(x => x.RoleId).ToListAsync();

        var rewardsToAdd = anubisRewards.ExceptBy(grimoireRewards, x => x.RoleId);

        await grimoireDbContext.BulkInsertAsync(rewardsToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateRolesAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisRoles = await this._anubisContext.IgnoredRoles
            .Select(x => new Role
            {
                GuildId = x.GuildId,
                Id = x.RoleId
            }).ToListAsync();

        anubisRoles.AddRange(await this._anubisContext.Rewards.Select(x => new Role
        {
            GuildId = x.GuildId,
            Id = x.RewardRole
        }).ToListAsync());


        var grimoireRoles = await grimoireDbContext.Roles.Select(x => x.Id).ToListAsync();
        var rolesToAdd = anubisRoles.Distinct().ExceptBy(grimoireRoles, x => x.Id);

        await grimoireDbContext.BulkInsertAsync(rolesToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateUserXpAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisMembers = await this._anubisContext.UserLevels.ToListAsync();

        var grimoireMembers = await grimoireDbContext.Members
            .Where(x => x.XpHistory.Any(x => x.Type == XpHistoryType.Migrated))
            .Select(x => new { x.UserId, x.GuildId }).ToListAsync();

        var userXpToAdd = anubisMembers.ExceptBy(grimoireMembers,x => new { x.UserId, x.GuildId })
            .Select(x => new XpHistory
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                Type = XpHistoryType.Migrated,
                Xp = x.Xp,
                TimeOut = x.Timeout.ToUniversalTime()
            });

        await grimoireDbContext.AddRangeAsync(userXpToAdd);
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateMembersAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisMembers = await this._anubisContext.UserLevels
            .Select(x => new Member
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new() {
                            UserId = x.UserId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync();
        var grimoireMembers = await grimoireDbContext.Members
            .Select(x => new { x.UserId, x.GuildId }).ToListAsync();
        var membersToAdd = anubisMembers.ExceptBy(grimoireMembers,x => new { x.UserId, x.GuildId });

        await grimoireDbContext.AddRangeAsync(membersToAdd);
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateUsersAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisMembers = await this._anubisContext.UserLevels
            .Select(x => new User
            {
                Id = x.UserId
            }).ToListAsync();
        var grimoireMembers = await grimoireDbContext.Users
            .Select(x => x.Id).ToListAsync();
        var usersToAdd = anubisMembers.ExceptBy(grimoireMembers,x => x.Id);

        await grimoireDbContext.BulkInsertAsync(usersToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateSettingsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var anubisSettings = await this._anubisContext.LevelSettings.ToListAsync();

        foreach (var anubisGuild in anubisSettings)
        {
            var grimoireGuild = await grimoireDbContext.Guilds
                .Include(x => x.LevelSettings)
                .Include(x => x.Channels)
                .FirstOrDefaultAsync(guild => guild.Id == anubisGuild.GuildId);

            if (grimoireGuild is null)
            {
                grimoireGuild = new Guild
                {
                    Id = anubisGuild.GuildId,
                    LevelSettings = new GuildLevelSettings(),
                    ModerationSettings = new GuildModerationSettings(),
                    UserLogSettings = new GuildUserLogSettings(),
                    MessageLogSettings = new GuildMessageLogSettings()
                };
                await grimoireDbContext.AddAsync(grimoireGuild);
                await grimoireDbContext.SaveChangesAsync();
            }

            grimoireGuild.UpdateGuildLevelSettings(anubisGuild);

            grimoireDbContext.Update(grimoireGuild);
            await grimoireDbContext.SaveChangesAsync();
        }
    }
}
