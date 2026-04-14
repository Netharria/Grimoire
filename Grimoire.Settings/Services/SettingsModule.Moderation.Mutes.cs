// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using EntityFramework.Exceptions.Common;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<RoleId?> GetEffectiveMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Moderation, guildId, cancellationToken))
            return null;
        return await GetConfiguredMuteRole(guildId, cancellationToken);
    }

    public async Task<RoleId?> GetConfiguredMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ParseRoleId(await GetGuildSetting(GuildSettingType.MuteRole, guildId, cancellationToken));

    public Task<SettingsResult> DisableMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => SetGuildSetting(
            new GuildSettingDisabled
            {
                GuildId = guildId,
                Type = GuildSettingType.MuteRole,
                SetBy = moderatorId,
                SetAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

    public Task<SettingsResult> SetMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        RoleId muteRoleId,
        CancellationToken cancellationToken = default)
        => SetGuildSetting(
            new GuildSettingCustomValue
            {
                GuildId = guildId,
                Type = GuildSettingType.MuteRole,
                SetBy = moderatorId,
                SetAt = DateTimeOffset.UtcNow,
                Value = muteRoleId.Value.ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);

    public async Task<bool> IsMemberMuted(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Mutes
            .AsNoTracking()
            .AnyAsync(x =>
                    x.UserId == userId
                    && x.GuildId == guildId
                    && x.EndTime > DateTimeOffset.UtcNow,
                cancellationToken);
    }

    public async Task<SettingsResult> AddMute(
        UserId userId,
        GuildId guildId,
        SinId sinId,
        DateTimeOffset muteEndTime,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

        var updated = await dbContext.Mutes
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.EndTime, muteEndTime)
                    .SetProperty(x => x.SinId, sinId),
                cancellationToken);

        if (updated > 0)
            return SettingsResult.Written();

        dbContext.Mutes.Add(new Mute { UserId = userId, GuildId = guildId, EndTime = muteEndTime, SinId = sinId });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SettingsResult.Written();
        }
        catch (UniqueConstraintException)
        {
            await dbContext.Mutes
                .Where(x => x.UserId == userId && x.GuildId == guildId)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.EndTime, muteEndTime)
                        .SetProperty(x => x.SinId, sinId),
                    cancellationToken);
            return SettingsResult.Written();
        }
    }

    public async Task<SettingsResult<Mute?>> RemoveMute(UserId userId, GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingMute = await dbContext.Mutes
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingMute is null)
            return SettingsResult.Unchanged<Mute?>(null);
        await dbContext.Mutes
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);
        return SettingsResult.Written<Mute?>(existingMute);
    }

    public async IAsyncEnumerable<Mute> GetAllExpiredMutes(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredMute in dbContext.Mutes
                           .AsNoTracking()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredMute;
    }

    public async IAsyncEnumerable<Mute> GetAllMutes(GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var mute in dbContext.Mutes
                           .AsNoTracking()
                           .Where(mute => mute.GuildId == guildId)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return mute;
    }
}
