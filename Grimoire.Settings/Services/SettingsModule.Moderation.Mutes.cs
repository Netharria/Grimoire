// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<Result<RoleId?>> GetEffectiveMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Moderation, guildId, cancellationToken)
                .GetOrElse(() => false))
            return Result<RoleId?>.Ok(null);
        return await GetConfiguredMuteRole(guildId, cancellationToken);
    }

    public Task<Result<RoleId?>> GetConfiguredMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetGuildSetting(GuildSettingType.MuteRole, guildId, cancellationToken)
            .AsTask()
            .Map(ParseRoleId);

    public Task<Result<GuildId>> DisableMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => GuildSettingDisabled.Create(GuildSettingType.MuteRole, guildId, moderatorId, DateTimeOffset.UtcNow)
            .ToResult()
            .BindAsync(
                setting => SetGuildSetting(setting, cancellationToken).Map(_ => guildId));

    public Task<Result<RoleId>> SetMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        RoleId muteRoleId,
        CancellationToken cancellationToken = default)
        => GuildSettingCustomValue.Create(GuildSettingType.MuteRole, guildId, moderatorId, DateTimeOffset.UtcNow,
                muteRoleId.Value.ToString(CultureInfo.InvariantCulture))
            .ToResult()
            .BindAsync(
                setting => SetGuildSetting(setting, cancellationToken).Map(_ => muteRoleId));

    public async Task<Result<bool>> IsMemberMuted(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        return Result<bool>.Ok(await dbContext.Mutes
            .AsNoTracking()
            .OfType<MuteAdded>()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.Mutes.Any(y =>
                y.UserId == x.UserId && y.GuildId == x.GuildId && y.SetAt > x.SetAt))
            .AnyAsync(x => x.EndTime > DateTimeOffset.UtcNow, cancellationToken));
    }

    public async Task<Result<MuteAdded>> AddMute(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        SinId sinId,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        var setAt = DateTimeOffset.UtcNow;
        if (MuteAdded.Create(userId, guildId, moderatorId, sinId, setAt, endTime)
            is not Validation<MuteAdded>.Valid(var mute))
            return Result<MuteAdded>.Fail(new Error("mute.invalid", "Mute parameters are invalid."));

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Mutes.Add(mute);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<MuteAdded>.Ok(mute);
    }

    public async Task<Result<MuteAdded>> RemoveMute(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingMute = await dbContext.Mutes
            .AsNoTracking()
            .OfType<MuteAdded>()
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            // ReSharper disable once AccessToDisposedClosure
            .Where(x => !dbContext.Mutes.Any(y =>
                y.UserId == x.UserId && y.GuildId == x.GuildId && y.SetAt > x.SetAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (existingMute is null)
            return new Result<MuteAdded>.NotFound(
                new Error("mute.not-found", "No active mute found for this user."));

        if (MuteRemoved.Create(userId, guildId, moderatorId, DateTimeOffset.UtcNow)
            is not Validation<MuteRemoved>.Valid(var muteRemoved))
            return Result<MuteAdded>.Fail(
                new Error("mute-removed.invalid", "Unable to create mute removal event."));
        dbContext.Mutes.Add(muteRemoved);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<MuteAdded>.Ok(existingMute);
    }

    public async IAsyncEnumerable<MuteAdded> GetAllExpiredMutes(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var expiredMute in dbContext.Mutes
                           .AsNoTracking()
                           .OfType<MuteAdded>()
                           .Where(x => x.EndTime <= DateTimeOffset.UtcNow)
                           // ReSharper disable once AccessToDisposedClosure
                           .Where(x => !dbContext.Mutes.Any(y =>
                               y.UserId == x.UserId && y.GuildId == x.GuildId && y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return expiredMute;
    }

    public async IAsyncEnumerable<MuteAdded> GetAllMutes(
        GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var mute in dbContext.Mutes
                           .AsNoTracking()
                           .OfType<MuteAdded>()
                           .Where(x => x.GuildId == guildId)
                           // ReSharper disable once AccessToDisposedClosure
                           .Where(x => !dbContext.Mutes.Any(y =>
                               y.UserId == x.UserId && y.GuildId == x.GuildId && y.SetAt > x.SetAt))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return mute;
    }
}
