// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class DiscordInviteExtensions
{
    extension(DiscordInvite invite)
    {
        [Pure]
        public InviteUrl GetInviteUrl() => InviteUrl.FromDatabase(invite.ToString());

        [Pure]
        public InviteCode GetInviteCode() => InviteCode.FromDatabase(invite.Code);

        /// <remarks>Discord leaves out the inviter for some invites, such as those made by an integration.</remarks>
        [Pure]
        public Invite ToInvite() => new()
        {
            Code = invite.GetInviteCode(),
            Inviter = invite.Inviter?.GetUsername(),
            Url = invite.GetInviteUrl(),
            Uses = invite.Uses,
            MaxUses = invite.MaxUses
        };
    }
}
