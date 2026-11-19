// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Rows;
using Microsoft.EntityFrameworkCore;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    public async Task<Fin<Option<RoleId>>> GetEffectiveMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.Moderation, guildId, cancellationToken)
                .OrDefaultDuringMigration(false))
            return Option<RoleId>.None;
        return await GetConfiguredMuteRole(guildId, cancellationToken);
    }

    public Task<Fin<Option<RoleId>>> GetConfiguredMuteRole(
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => GetDocument<ModerationConfig>(guildId, cancellationToken)
            .MapFin(moderation => Optional(moderation.MuteRole));

    /// <summary>No command turns the mute role off; the integration tests use this to reach the unset state.</summary>
    internal Task<Fin<Write<GuildId>>> DisableMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => UpdateDocument<ModerationConfig>(guildId, moderatorId,
                moderation => moderation with { MuteRole = null }, cancellationToken)
            .MapFin(write => write.Map(_ => guildId));

    public Task<Fin<Write<RoleId>>> SetMuteRole(
        GuildId guildId,
        ModeratorId moderatorId,
        RoleId muteRoleId,
        CancellationToken cancellationToken = default)
        => UpdateDocument<ModerationConfig>(guildId, moderatorId,
                moderation => moderation with { MuteRole = muteRoleId }, cancellationToken)
            .MapFin(write => write.Map(_ => muteRoleId));

    /// <summary>
    ///     Records a mute that has been applied, as Active. A mute of a muted member replaces the one in force, which
    ///     is Removed by this mute's moderator, in the same transaction. 3.4's processor records Pending instead.
    /// </summary>
    public Task<Fin<Mute>> AddMute(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        SinId sinId,
        DateTimeOffset endTime,
        RoleId? muteRoleId,
        CancellationToken cancellationToken = default)
        => NewMute.Create(userId, guildId, moderatorId, sinId, DateTimeOffset.UtcNow, endTime, muteRoleId) switch
        {
            Validation<Error, NewMute>.Success(var mute) => ExecuteSafelyAsync(async ct =>
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
                var row = await InTransaction(dbContext, async innerCt =>
                {
                    await EndInForce(MemberMutes(dbContext, userId, guildId),
                        new ItemState.Removed(mute.SetAt, moderatorId, null), innerCt);
                    var added = new MuteRow
                    {
                        GuildId = mute.GuildId,
                        UserId = mute.UserId,
                        SinId = mute.SinId,
                        MuteRoleId = mute.MuteRoleId,
                        ModeratorId = mute.ModeratorId,
                        SetAt = mute.SetAt,
                        EndTime = mute.EndTime,
                        State = nameof(ItemState.Active),
                        ActivatedAt = mute.SetAt
                    };
                    dbContext.Mutes.Add(added);
                    await dbContext.SaveChangesAsync(innerCt);
                    return added;
                }, ct);
                return Fin.Succ<Mute>(row.ToMute());
            }, "Could not save the mute.", cancellationToken),
            _ => Task.FromResult(Fin.Fail<Mute>(new Invalid("Mute parameters are invalid.")))
        };

    /// <summary>The member's mute in force, past its end or not, without changing anything.</summary>
    public Task<Fin<Mute>> GetActiveMute(
        UserId userId,
        GuildId guildId,
        CancellationToken cancellationToken = default)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            return await FindInForceMute(dbContext, userId, guildId, ct);
        }, "Could not look up the member's mute.", cancellationToken);

    /// <summary>A moderator ends the member's mute: it becomes Removed. NotFound when nothing is in force.</summary>
    public Task<Fin<Mute>> RemoveMute(
        UserId userId,
        GuildId guildId,
        ModeratorId moderatorId,
        CancellationToken cancellationToken = default)
        => EndMute(userId, guildId, new ItemState.Removed(DateTimeOffset.UtcNow, moderatorId, null),
            "Could not remove the mute.", cancellationToken);

    /// <summary>The mute ran out: it becomes Expired. NotFound when it already ended (a race with an unmute).</summary>
    public Task<Fin<Mute>> ExpireMute(
        Mute mute,
        CancellationToken cancellationToken = default)
        => EndMute(mute.UserId, mute.GuildId, new ItemState.Expired(DateTimeOffset.UtcNow),
            "Could not record the expired mute.", cancellationToken);

    private Task<Fin<Mute>> EndMute(UserId userId, GuildId guildId, ItemState ended, string failure,
        CancellationToken cancellationToken)
        => ExecuteSafelyAsync(async ct =>
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(ct);
            return await FindInForceMute(dbContext, userId, guildId, ct)
                .BindAsync(async mute =>
                    await EndInForce(dbContext.Mutes.Where(row => row.SinId == mute.SinId), ended, ct) == 1
                        ? Fin.Succ<Mute>(mute with { State = ended })
                        : new NotFound("No active mute found for this user."));
        }, failure, cancellationToken);

    private static async Task<Fin<Mute>> FindInForceMute(SettingsDbContext dbContext, UserId userId,
        GuildId guildId, CancellationToken cancellationToken)
        => await MemberMutes(dbContext, userId, guildId)
                .Where(InForce<MuteRow>())
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken) switch
            {
                { } row => Fin.Succ<Mute>(row.ToMute()),
                null => new NotFound("No active mute found for this user.")
            };

    private static IQueryable<MuteRow> MemberMutes(SettingsDbContext dbContext, UserId userId, GuildId guildId)
        => dbContext.Mutes.Where(row => row.UserId == userId && row.GuildId == guildId);

    /// <summary>Applied mutes past their end, for the expiry poll (IX_Mutes_EndTime).</summary>
    public async IAsyncEnumerable<Mute> GetAllExpiredMutes(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Read everything up front so no database connection is held open while the caller talks to Discord.
        List<MuteRow> expiredMutes;
        await using (var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            expiredMutes = await dbContext.Mutes
                .AsNoTracking()
                .Where(row => row.State == nameof(ItemState.Active) && row.EndTime <= now)
                .ToListAsync(cancellationToken);
        }

        foreach (var expiredMute in expiredMutes)
            yield return expiredMute.ToMute();
    }

    /// <summary>The guild's mutes in force.</summary>
    public async IAsyncEnumerable<Mute> GetAllMutes(
        GuildId guildId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var row in dbContext.Mutes
                           .AsNoTracking()
                           .Where(row => row.GuildId == guildId)
                           .Where(InForce<MuteRow>())
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
            yield return row.ToMute();
    }
}
