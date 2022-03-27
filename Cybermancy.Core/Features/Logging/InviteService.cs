// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Cybermancy.Domain;

namespace Cybermancy.Core.Features.Logging
{
    public interface IInviteService
    {
        Invite? CalculateInviteUsed(IEnumerable<Invite> guildInvites);
        void UpdateInvite(Invite invite);
    }

    public class InviteService : IInviteService
    {
        private readonly ConcurrentDictionary<string, Invite> _invites = new();

        public void UpdateInvite(Invite invite)
        {
            if (invite is null) throw new ArgumentNullException(nameof(invite));
            _invites[invite.Code] = invite;
        }

        public Invite? CalculateInviteUsed(IEnumerable<Invite> guildInvites)
        {
            foreach (var invite in guildInvites)
            {
                _invites.TryGetValue(invite.Code, out var inviteUsed);
                if (inviteUsed is null) continue;
                if (invite.Uses == inviteUsed.Uses) continue;
                _invites[invite.Code] = invite;
                return invite;
            }
            return null;
        }
    }
}
