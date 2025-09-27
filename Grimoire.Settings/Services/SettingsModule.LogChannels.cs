// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{

    public async Task<ulong?> GetLogChannelSetting(GuildLogType guildLogType, ulong guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await this.IsModuleEnabled(guildLogType.GetLogTypeModule(), guildId, cancellationToken))
            return null;

        var logChannelSetting = await this._memoryCache.GetOrCreateAsync(guildLogType.GetCacheKey(guildId),
            async _ =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                var channelId = guildLogType switch
                {
                    GuildLogType.Moderation => await dbContext.GuildSettings
                        .Where(settings => settings.Id == guildId)
                        .Select(settings => settings.ModLogChannelId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.Leveling => await dbContext.GuildLevelSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.LevelChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.BulkMessageDeleted => await dbContext.GuildMessageLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.BulkDeleteChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.MessageEdited => await dbContext.GuildMessageLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.EditChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.MessageDeleted => await dbContext.GuildMessageLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.DeleteChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.UserJoined => await dbContext.GuildUserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.JoinChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.UserLeft => await dbContext.GuildUserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.LeaveChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.AvatarUpdated => await dbContext.GuildUserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.AvatarChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.NicknameUpdated => await dbContext.GuildUserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.NicknameChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    GuildLogType.UsernameUpdated => await dbContext.GuildUserLogSettings
                        .Where(settings => settings.GuildId == guildId)
                        .Select(settings => settings.UsernameChannelLogId)
                        .FirstOrDefaultAsync(cancellationToken),
                    _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, "Unknown log type")
                };

                return new GuildLogCacheEntry
                {
                    ChannelId = channelId,
                };
            }, this._cacheEntryOptions);

        return logChannelSetting.ChannelId;
    }

    public async Task SetLogChannelSetting(GuildLogType guildLogType, ulong guildId,
        ulong? channelId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        switch (guildLogType)
        {
            case GuildLogType.Moderation:
                var guildSettings = await dbContext.GuildSettings
                    .FirstOrDefaultAsync(settings => settings.Id == guildId, cancellationToken)
                    ?? new GuildSettings{ Id = guildId };
                guildSettings.ModLogChannelId = channelId;
                await dbContext.AddAsync(guildSettings, cancellationToken);
                break;
            case GuildLogType.Leveling:
                var levelSettings = await dbContext.GuildLevelSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new LevelingSettings{ GuildId = guildId };
                levelSettings.LevelChannelLogId = channelId;
                await dbContext.AddAsync(levelSettings, cancellationToken);
                break;
            case GuildLogType.MessageEdited:
                var messageLogSettings = await dbContext.GuildMessageLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new MessageLogSettings{ GuildId = guildId };
                messageLogSettings.EditChannelLogId = channelId;
                await dbContext.AddAsync(messageLogSettings, cancellationToken);
                break;
            case GuildLogType.MessageDeleted:
                var messageLogSettings2 = await dbContext.GuildMessageLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new MessageLogSettings{ GuildId = guildId };
                messageLogSettings2.DeleteChannelLogId = channelId;
                await dbContext.AddAsync(messageLogSettings2, cancellationToken);
                break;
            case GuildLogType.BulkMessageDeleted:
                var messageLogSettings3 = await dbContext.GuildMessageLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new MessageLogSettings{ GuildId = guildId };
                messageLogSettings3.BulkDeleteChannelLogId = channelId;
                await dbContext.AddAsync(messageLogSettings3, cancellationToken);
                break;
            case GuildLogType.UserJoined:
                var userLogSettings = await dbContext.GuildUserLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new UserLogSettings{ GuildId = guildId };
                userLogSettings.JoinChannelLogId = channelId;
                await dbContext.AddAsync(userLogSettings, cancellationToken);
                break;
            case GuildLogType.UserLeft:
                var userLogSettings2 = await dbContext.GuildUserLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new UserLogSettings{ GuildId = guildId };
                userLogSettings2.LeaveChannelLogId = channelId;
                await dbContext.AddAsync(userLogSettings2, cancellationToken);
                break;
            case GuildLogType.AvatarUpdated:
                var userLogSettings3 = await dbContext.GuildUserLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new UserLogSettings{ GuildId = guildId };
                userLogSettings3.AvatarChannelLogId = channelId;
                await dbContext.AddAsync(userLogSettings3, cancellationToken);
                break;
            case GuildLogType.NicknameUpdated:
                var userLogSettings4 = await dbContext.GuildUserLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new UserLogSettings{ GuildId = guildId };
                userLogSettings4.NicknameChannelLogId = channelId;
                await dbContext.AddAsync(userLogSettings4, cancellationToken);
                break;
            case GuildLogType.UsernameUpdated:
                var userLogSettings5 = await dbContext.GuildUserLogSettings
                    .FirstOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
                    ?? new UserLogSettings{ GuildId = guildId };
                userLogSettings5.UsernameChannelLogId = channelId;
                await dbContext.AddAsync(userLogSettings5, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, null);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(guildLogType.GetCacheKey(guildId));
    }

    public async Task<ulong?> GetUserCommandChannel(ulong guildId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"UserCommandChannel-{guildId}";

        var logChannelSetting = await this._memoryCache.GetOrCreateAsync<GuildLogCacheEntry>(cacheKey,
            async _ =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                var channelId = await dbContext.GuildSettings
                    .Where(settings => settings.Id == guildId)
                    .Select(settings => settings.UserCommandChannelId)
                    .FirstOrDefaultAsync(cancellationToken);

                return new GuildLogCacheEntry
                {
                    ChannelId = channelId,
                };
            }, this._cacheEntryOptions);

        return logChannelSetting?.ChannelId;
    }

    public async Task SetUserCommandChannelSetting(ulong guildId, ulong? channelId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"UserCommandChannel-{guildId}";

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var guildSettings = await dbContext.GuildSettings
            .Where(settings => settings.Id == guildId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? new GuildSettings{ Id = guildId };
        guildSettings.UserCommandChannelId = channelId;
        await dbContext.AddAsync(guildSettings, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        this._memoryCache.Remove(cacheKey);
    }

    private record GuildLogCacheEntry
    {
        public required ulong? ChannelId { get; init; }
    }
}
