// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    private const string LogOverridesCacheKeyPrefix = "LogOverides_{0}";

    public async Task<bool> ShouldLogMessage(ChannelId channelId,
        GuildId guildId,
        IReadOnlyDictionary<ChannelId, ChannelId?> channelNodes,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.MessageLog, guildId, cancellationToken))
            return false;

        ChannelId? currentChannelId = channelId;
        while (currentChannelId is not null)
        {
            var overrideOption = await GetChannelLogOverride(currentChannelId.Value, guildId, cancellationToken);
            switch (overrideOption)
            {
                case MessageLogOverrideCacheOption.AlwaysLog:
                    return true;
                case MessageLogOverrideCacheOption.NeverLog:
                    return false;
                case MessageLogOverrideCacheOption.Inherit:
                default:
                    if (channelNodes.TryGetValue(currentChannelId.Value, out var node))
                        currentChannelId = node;
                    break;
            }
        }

        return true;
    }

    private async Task<MessageLogOverrideCacheOption> GetChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken)
    {
        var cacheKey = string.Format(LogOverridesCacheKeyPrefix, channelId);
        return await this._memoryCache.GetOrCreateAsync(cacheKey, async _ =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var channelOverride = await dbContext.MessagesLogChannelOverrides
                .AsNoTracking()
                .Where(ovr => ovr.GuildId == guildId && ovr.ChannelId == channelId)
                .Select(ovr => ovr.ChannelOption)
                .FirstOrDefaultAsync(cancellationToken);
            return channelOverride switch
            {
                MessageLogOverrideOption.AlwaysLog => MessageLogOverrideCacheOption.AlwaysLog,
                MessageLogOverrideOption.NeverLog => MessageLogOverrideCacheOption.NeverLog,
                _ => MessageLogOverrideCacheOption.Inherit
            };
        }, this._cacheEntryOptions);
    }

    public async Task SetChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        MessageLogOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingOverride = await dbContext.MessagesLogChannelOverrides
                                   .Where(ovr => ovr.GuildId == guildId && ovr.ChannelId == channelId)
                                   .FirstOrDefaultAsync(cancellationToken)
                               ?? new MessageLogChannelOverride { GuildId = guildId, ChannelId = channelId };

        existingOverride.ChannelOption = option;

        dbContext.MessagesLogChannelOverrides.Add(existingOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = string.Format(LogOverridesCacheKeyPrefix, channelId);
        this._memoryCache.Remove(cacheKey);
    }

    public async Task RemoveChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingOverride = await dbContext.MessagesLogChannelOverrides
            .Where(ovr => ovr.GuildId == guildId && ovr.ChannelId == channelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingOverride is null)
            return;

        dbContext.MessagesLogChannelOverrides.Remove(existingOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cacheKey = string.Format(LogOverridesCacheKeyPrefix, channelId);
        this._memoryCache.Remove(cacheKey);
    }

    public async IAsyncEnumerable<MessageLogChannelOverride> GetAllOverriddenChannels(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var overrides = dbContext.MessagesLogChannelOverrides
            .AsNoTracking()
            .Where(ovr => ovr.GuildId == guildId)
            .AsAsyncEnumerable();

        await foreach (var channelId in overrides.WithCancellation(cancellationToken)) yield return channelId;
    }


    private enum MessageLogOverrideCacheOption
    {
        NeverLog,
        AlwaysLog,
        Inherit
    }
}
