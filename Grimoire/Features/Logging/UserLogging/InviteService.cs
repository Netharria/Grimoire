// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Features.Logging.UserLogging;

public interface IInviteService
{
    Invite? CalculateInviteUsed(GuildInviteDto guildInvites);
    void UpdateInvite(ulong guildId, Invite invite);
    void UpdateGuildInvites(GuildInviteDto guildInvites);
    void UpdateAllInvites(List<GuildInviteDto> guildInvites);
    bool DeleteInvite(ulong guildId, string inviteCode);
}

public sealed class InviteService : IInviteService
{
    private readonly ConcurrentDictionary<ulong, GuildInviteDto> _guilds = new();

    public void UpdateInvite(ulong guildId, Invite invite)
    {
        if (!this._guilds.TryGetValue(guildId, out var guild))
            throw new ArgumentException("Could not find guild.");
        guild.Invites.AddOrUpdate(
            invite.Code,
            invite,
            (_, _) => invite);
    }

    public void UpdateGuildInvites(GuildInviteDto guildInvites)
        => this._guilds.AddOrUpdate(
            guildInvites.GuildId,
            guildInvites,
            (_, _) => guildInvites);

    public void UpdateAllInvites(List<GuildInviteDto> guildInvites)
        => guildInvites.ForEach(guild =>
            this._guilds.AddOrUpdate(
                guild.GuildId,
                guild,
                (_, _) => guild));

    public Invite? CalculateInviteUsed(GuildInviteDto guildInvites)
    {
        if (!this._guilds.TryGetValue(guildInvites.GuildId, out var guild))
            throw new ArgumentException("Could not find guild.");

        var newInvites = guildInvites.Invites;
        var existingInvites = guild.Invites;

        var inviteUsed = newInvites
            .Where(x =>
                !existingInvites.TryGetValue(x.Key, out var existingInvite)
                || x.Value.Uses != existingInvite.Uses)
            .Select(x => x.Value)
            .FirstOrDefault();
        if (inviteUsed is not null)
        {
            UpdateInvite(guildInvites.GuildId, inviteUsed);
            return inviteUsed;
        }

        inviteUsed = existingInvites
            .Where(invite =>
                !newInvites.TryGetValue(invite.Key, out var newInvite)
                || invite.Value.Uses != newInvite.Uses)
            .Select(invite => invite.Value)
            .FirstOrDefault();
        if (inviteUsed is null || inviteUsed.Uses + 1 != inviteUsed.MaxUses)
            return null;
        if (!DeleteInvite(guildInvites.GuildId, inviteUsed.Code))
            throw new Exception("Was not able to delete invite.");
        return inviteUsed;
    }

    public bool DeleteInvite(ulong guildId, string inviteCode)
    {
        if (!this._guilds.TryGetValue(guildId, out var guild))
            throw new ArgumentException("Could not find guild.");
        return guild.Invites.TryRemove(inviteCode, out _);
    }
}

public sealed record GuildInviteDto
{
    public GuildId GuildId { get; init; }
    public ConcurrentDictionary<string, Invite> Invites { get; init; } = new();
}
