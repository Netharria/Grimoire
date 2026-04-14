// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
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
                case MessageLogOverrideOption.AlwaysLog:
                    return true;
                case MessageLogOverrideOption.NeverLog:
                    return false;
                default:
                    currentChannelId = channelNodes.GetValueOrDefault(currentChannelId.Value);
                    break;
            }
        }

        return true;
    }

    private async Task<MessageLogOverrideOption> GetChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        CancellationToken cancellationToken)
        =>
            await this._cache.GetOrCreateAsync(CacheKey.LogOverride(channelId),
                new { channelId, guildId },
                async (state, ct) =>
                {
                    await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                    var channelOverride = await dbContext.MessagesLogChannelOverrides
                        .AsNoTracking()
                        .Where(ovr => ovr.GuildId == state.guildId && ovr.ChannelId == state.channelId)
                        .OrderByDescending(x => x.SetAt)
                        .Select(ovr => (MessageLogOverrideOption?)ovr.ChannelOption)
                        .FirstOrDefaultAsync(ct);
                    return channelOverride ?? MessageLogOverrideOption.Inherit;
                }, this._cacheEntryOptions,
                cancellationToken: cancellationToken);

    public async Task<SettingsResult> SetChannelLogOverride(ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        MessageLogOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        var existingOverride = await GetChannelLogOverride(channelId, guildId, cancellationToken);

        if (existingOverride == option)
            return SettingsResult.Unchanged();

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        var newOverride = new MessageLogChannelOverride
        {
            ChannelId = channelId,
            GuildId = guildId,
            SetBy = setBy,
            ChannelOption = option,
            SetAt = DateTimeOffset.UtcNow
        };


        dbContext.MessagesLogChannelOverrides.Add(newOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        await this._cache.SetAsync(CacheKey.LogOverride(channelId),
            option, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
        return SettingsResult.Written();
    }

    public async IAsyncEnumerable<MessageLogChannelOverride> GetAllOverriddenChannels(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var overrides = dbContext.MessagesLogChannelOverrides
            .AsNoTracking()
            .Where(ovr => ovr.GuildId == guildId)
            .GroupBy(ovr => ovr.ChannelId)
            .Select(ovr =>
                ovr
                    .OrderByDescending(x => x.SetAt)
                    .First())
            .Where(x => x.ChannelOption != MessageLogOverrideOption.Inherit)
            .AsAsyncEnumerable();

        await foreach (var channelId in overrides.WithCancellation(cancellationToken)) yield return channelId;
    }
}
