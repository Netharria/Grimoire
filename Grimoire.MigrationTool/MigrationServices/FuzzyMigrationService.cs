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

public class FuzzyMigrationService
{
    private readonly FuzzyDbContext _fuzzyDbContext;

    public FuzzyMigrationService(FuzzyDbContext fuzzyDbContext)
    {
        this._fuzzyDbContext = fuzzyDbContext;
    }

    public async Task MigrateFuzzyDatabaseAsync()
    {
        await this.MigrateModerationSettingsAsync();
        await this.MigrateUsersAsync();
        await this.MigrateMembersAsync();
        await this.MigrateChannelsAsync();
        await this.MigrateInfractionsAsync();
        await this.MigrateLocksAsync();
    }

    private async Task MigrateLocksAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var locks = await this._fuzzyDbContext
        .Locks
        .Select(x => new Lock
        {
            ChannelId = x.ChannelId,
            PreviouslyAllowed = x.PreviousValue ? 0x0000000000000800 : 0x0000000000000000,
            ModeratorId = x.ModeratorId,
            GuildId = x.GuildId,
            Reason = x.Reason ?? string.Empty,
            EndTime = x.EndTime.ToUniversalTime()
        }).ToListAsync();
        locks.AddRange(await this._fuzzyDbContext
        .ThreadLocks
        .Select(x => new Lock
        {
            ChannelId = x.ChannelId,
            ModeratorId = x.ModeratorId,
            GuildId = x.GuildId,
            Reason = x.Reason ?? string.Empty,
            EndTime = x.EndTime.ToUniversalTime()
        }).ToListAsync());

        var grimoireLocks = await grimoireDbContext.Locks
            .Select(x => x.ChannelId).ToListAsync();

        var locksToAdd = locks.ExceptBy(grimoireLocks, x => x.ChannelId);
        await grimoireDbContext.BulkInsertAsync(locksToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateInfractionsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var infractions = await this._fuzzyDbContext
            .Infractions
            .Include(x => x.Pardon)
            .Include(x => x.Mute)
            .ToListAsync();
        var sins = infractions.Select(x =>
        {
            var sin = new Sin
            {
                Id = x.Id,
                UserId = x.UserId,
                GuildId = x.GuildId,
                ModeratorId = x.ModeratorId == 0 ? null : x.ModeratorId,
                Reason = x.Reason  ?? "",
                SinType = x.InfractionType switch
                {
                    "Warn" => SinType.Warn,
                    "Mute" => SinType.Mute,
                    "Ban" => SinType.Ban,
                    _ => throw new ArgumentOutOfRangeException()
                },
                SinOn = x.InfractionOn.ToUniversalTime(),
            };
            sin.Reason = sin.Reason.Length > 1000 ? sin.Reason[..1000] : sin.Reason;
            if (x.Pardon is not null)
            {
                sin.Pardon = new Pardon
                {
                    GuildId = x.GuildId,
                    ModeratorId = x.Pardon.ModeratorId,
                    SinId = x.Id,
                    Reason = x.Pardon.Reason ?? "",
                    PardonDate = x.Pardon.PardonOn.ToUniversalTime()
                };
            }
            if(x.Mute is not null)
            {
                sin.Mute = new Mute
                {
                    SinId = x.Id,
                    UserId= x.UserId,
                    GuildId = x.GuildId,
                    EndTime = x.Mute.EndTime.ToUniversalTime()
                };
            }
            return sin;
        }).ToList();


        var grimoireSins = await grimoireDbContext.Sins
            .Select(x => x.Id).ToListAsync();

        var sinsToAdd = sins.ExceptBy(grimoireSins, x => x.Id);
        await grimoireDbContext.AddRangeAsync(sinsToAdd);
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateChannelsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var fuzzyChannels = await this._fuzzyDbContext
            .Locks
            .Select(x => new Channel
            {
                Id = x.ChannelId,
                GuildId = x.GuildId
            }).ToListAsync();
        fuzzyChannels.AddRange(await this._fuzzyDbContext
            .ThreadLocks
            .Select(x => new Channel
            {
                Id = x.ChannelId,
                GuildId = x.GuildId
            }).ToListAsync());

        var grimoireChannels = await grimoireDbContext.Channels.Select(x => x.Id).ToListAsync();

        var channelsToAdd = fuzzyChannels.ExceptBy(grimoireChannels, x => x.Id);
        await grimoireDbContext.BulkInsertAsync(channelsToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }
    private async Task MigrateMembersAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var fuzzyUsers = await this._fuzzyDbContext
            .Infractions
            .Select(x => new Member
            {
                UserId = x.UserId,
                GuildId = x.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = x.UserId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync();
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Infractions
            .Where(x => x.ModeratorId != 0)
            .Select(x => new Member
            {
                UserId = x.ModeratorId,
                GuildId = x.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = x.ModeratorId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Pardons
            .Select(x => new Member
            {
                UserId = x.ModeratorId,
                GuildId = x.Infraction.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = x.ModeratorId,
                            GuildId = x.Infraction.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Locks
            .Select(x => new Member
            {
                UserId = x.ModeratorId,
                GuildId = x.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = x.ModeratorId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .ThreadLocks
            .Select(x => new Member
            {
                UserId = x.ModeratorId,
                GuildId = x.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new XpHistory
                        {
                            UserId = x.ModeratorId,
                            GuildId = x.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTimeOffset.UtcNow
                        }
                    },
            }).ToListAsync());

        fuzzyUsers = fuzzyUsers.Distinct().ToList();
        var grimoireMembers = await grimoireDbContext.Members
            .Select(x => new { x.UserId, x.GuildId }).ToListAsync();
        var membersToAdd = fuzzyUsers.ExceptBy(grimoireMembers,x => new { x.UserId, x.GuildId });

        await grimoireDbContext.AddRangeAsync(membersToAdd);
        await grimoireDbContext.SaveChangesAsync();
    }

    private async Task MigrateUsersAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var fuzzyUsers = await this._fuzzyDbContext
            .Infractions
            .Select(x => new User
            {
                Id = x.UserId
            }).ToListAsync();
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Infractions
            .Where(x => x.ModeratorId != 0)
            .Select(x => new User
            {
                Id = x.ModeratorId
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Pardons
            .Select(x => new User
            {
                Id = x.ModeratorId
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .Locks
            .Select(x => new User
            {
                Id = x.ModeratorId
            }).ToListAsync());
        fuzzyUsers.AddRange(await this._fuzzyDbContext
            .ThreadLocks
            .Select(x => new User
            {
                Id = x.ModeratorId
            }).ToListAsync());

        fuzzyUsers = fuzzyUsers.Distinct().ToList();
        var grimoireUsers = await grimoireDbContext.Users
            .Select(x => x.Id).ToListAsync();
        var usersToAdd = fuzzyUsers.ExceptBy(grimoireUsers, x => x.Id);

        await grimoireDbContext.BulkInsertAsync(usersToAdd);
        await grimoireDbContext.BulkSaveChangesAsync();
    }

    private async Task MigrateModerationSettingsAsync()
    {
        using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
        var fuzzySettings = await this._fuzzyDbContext.ModerationSettings.ToListAsync();

        foreach (var fuzzyGuild in fuzzySettings)
        {
            var grimoireGuild = await grimoireDbContext.Guilds
                .Include(x => x.ModerationSettings)
                .Include(x => x.Channels)
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(guild => guild.Id == fuzzyGuild.Id);

            if (grimoireGuild is null)
            {
                grimoireGuild = new Guild
                {
                    Id = fuzzyGuild.Id,
                    LevelSettings = new GuildLevelSettings(),
                    ModerationSettings = new GuildModerationSettings(),
                    UserLogSettings = new GuildUserLogSettings(),
                    MessageLogSettings = new GuildMessageLogSettings()
                };
                await grimoireDbContext.AddAsync(grimoireGuild);
                await grimoireDbContext.SaveChangesAsync();
            }

            grimoireGuild.UpdateGuildModerationSettings(fuzzyGuild);

            grimoireDbContext.Update(grimoireGuild);
            await grimoireDbContext.SaveChangesAsync();
        }
    }
}
