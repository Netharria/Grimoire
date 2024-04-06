// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Core.Features.UserLogging;

public interface IInviteService
{
    Invite? CalculateInviteUsed(GuildInviteDto guildInvites);
    void UpdateInvite(ulong guildId, Invite invite);
    void UpdateGuildInvites(GuildInviteDto guildInvites);
    void UpdateAllInvites(List<GuildInviteDto> guildInvites);
    bool DeleteInvite(ulong guildId, string inviteCode);
}

internal sealed class InviteService : IInviteService
{
    private readonly ConcurrentDictionary<ulong, GuildInviteDto> _guilds = new();

    public void UpdateInvite(ulong guildId, Invite invite)
    {
        if (!this._guilds.TryGetValue(guildId, out var guild))
            throw new ArgumentException("Could not find guild.");
        guild.Invites.AddOrUpdate(
            invite.Code,
            invite,
            (code, existingInvite) => invite);
    }
    public void UpdateGuildInvites(GuildInviteDto guildInvites)
        => this._guilds.AddOrUpdate(
                    guildInvites.GuildId,
                    guildInvites,
                    (guildId, existingGuild) => guildInvites);

    public void UpdateAllInvites(List<GuildInviteDto> guildInvites)
        => guildInvites.ForEach(guild =>
                this._guilds.AddOrUpdate(
                    guild.GuildId,
                    guild,
                    (guildId, existingGuild) => guild));

    public Invite? CalculateInviteUsed(GuildInviteDto guildInvites)
    {
        if (!this._guilds.TryGetValue(guildInvites.GuildId, out var guild))
            throw new ArgumentException("Could not find guild.");
        var inviteUsed = guildInvites.Invites
            .Except(guild.Invites)
            .Select(x => x.Value)
            .FirstOrDefault();
        if (inviteUsed is not null)
        {
            this.UpdateInvite(guildInvites.GuildId, inviteUsed);
            return inviteUsed;
        }
        inviteUsed = guild.Invites
            .Except(guildInvites.Invites)
            .Select(x => x.Value)
            .FirstOrDefault();
        if (inviteUsed is not null
            && inviteUsed.Uses + 1 == inviteUsed.MaxUses)
        {
            if (!this.DeleteInvite(guildInvites.GuildId, inviteUsed.Code))
                throw new Exception("Was not able to delete invite.");
            return inviteUsed;
        }
        return null;
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
    public ulong GuildId { get; init; }
    public ConcurrentDictionary<string, Invite> Invites { get; init; } = new();
}
