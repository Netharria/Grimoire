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
    public async Task<Result<bool>> ShouldLogMessage(ChannelId channelId,
        GuildId guildId,
        IReadOnlyDictionary<ChannelId, ChannelId?> channelNodes,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.MessageLog, guildId, cancellationToken)
                .GetOrElse(() => false))
            return Result<bool>.Ok(false);

        ChannelId? currentChannelId = channelId;
        while (currentChannelId is not null)
        {
            var overrideOption = await GetChannelLogOverride(currentChannelId.Value, guildId, cancellationToken);
            switch (overrideOption)
            {
                case MessageLogOverrideOption.AlwaysLog:
                    return Result<bool>.Ok(true);
                case MessageLogOverrideOption.NeverLog:
                    return Result<bool>.Ok(false);
                case MessageLogOverrideOption.Inherit:
                default:
                    currentChannelId = channelNodes.GetValueOrDefault(currentChannelId.Value);
                    break;
            }
        }

        return Result<bool>.Ok(true);
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
                    var channelOverride = await dbContext.MessageLogChannelOverrides
                        .AsNoTracking()
                        .Where(ovr => ovr.GuildId == state.guildId && ovr.ChannelId == state.channelId)
                        .OrderByDescending(x => x.SetAt)
                        .Select(ovr => (MessageLogOverrideOption?)ovr.ChannelOption)
                        .FirstOrDefaultAsync(ct);
                    return channelOverride ?? MessageLogOverrideOption.Inherit;
                }, this._cacheEntryOptions,
                cancellationToken: cancellationToken);

    public async Task<Result<MessageLogChannelOverride>> SetChannelLogOverride(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        MessageLogOverrideOption option,
        CancellationToken cancellationToken = default)
    {
        var existingOverride = await GetChannelLogOverride(channelId, guildId, cancellationToken);

        if (existingOverride == option)
            return new Result<MessageLogChannelOverride>.NotModified(
                new Error("channel-log-override.not-changed", "The channel is already set to this log option."));

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (MessageLogChannelOverride.Create(option, channelId, guildId, setBy, DateTimeOffset.UtcNow)
            is not Validation<MessageLogChannelOverride>.Valid(var newOverride))
            return Result<MessageLogChannelOverride>.Fail(
                new Error("message-log-override.invalid", "Unable to create message log channel override."));

        dbContext.MessageLogChannelOverrides.Add(newOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        await this._cache.SetAsync(CacheKey.LogOverride(channelId),
            option, this._cacheEntryOptions,
            cancellationToken: cancellationToken);
        return Result<MessageLogChannelOverride>.Ok(newOverride);
    }

    public async IAsyncEnumerable<MessageLogChannelOverride> GetAllOverriddenChannels(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        // EF Core cannot translate a .Where() after GroupBy().Select(g => g.First()).
        // Stream rows as they arrive and filter in memory.
        await foreach (var channelOverride in dbContext.MessageLogChannelOverrides
                           .AsNoTracking()
                           .Where(ovr => ovr.GuildId == guildId)
                           .GroupBy(ovr => ovr.ChannelId)
                           .Select(ovr => ovr.OrderByDescending(x => x.SetAt).First())
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            if (channelOverride.ChannelOption != MessageLogOverrideOption.Inherit)
                yield return channelOverride;
    }
}
