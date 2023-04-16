// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Core.Features.Logging
{
    public interface IInviteService
    {
        Invite CalculateInviteUsed(List<Invite> guildInvites);
        void UpdateInvite(Invite invite);
        void UpdateAllInvites(List<Invite> guildInvites);
        void DeleteInvite(string inviteCode);
        void DeleteAllInvites(List<string> inviteCodes);
    }

    public class InviteService : IInviteService
    {
        private readonly ConcurrentDictionary<string, Invite> _invites = new();

        public void UpdateInvite(Invite invite)
        {
            if (invite is null) throw new ArgumentNullException(nameof(invite));
            this._invites.AddOrUpdate(
                invite.Code,
                invite,
                (code, existingInvite) =>
                {
                    if (invite.Url == existingInvite.Url)
                        existingInvite.Uses = invite.Uses;
                    else
                        throw new ArgumentException($"Duplicate invite codes not allowed - code: {code}");
                    return existingInvite;
                });
        }

        public void UpdateAllInvites(List<Invite> guildInvites) => guildInvites.ForEach(x => this.UpdateInvite(x));

        public Invite CalculateInviteUsed(List<Invite> guildInvites)
        {
            foreach (var invite in guildInvites)
            {
                this._invites.TryGetValue(invite.Code, out var inviteUsed);
                if (inviteUsed is not null
                    && invite.Uses == inviteUsed.Uses)
                    continue;
                this.UpdateInvite(invite);
                return invite;
            }
            return new Invite { Url = "Unknown Invite" };
        }

        public void DeleteInvite(string inviteCode) => this._invites.TryRemove(inviteCode, out _);

        public void DeleteAllInvites(List<string> inviteCodes) => inviteCodes.ForEach(x => this.DeleteInvite(x));
    }
}
