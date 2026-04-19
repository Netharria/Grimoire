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
        public InviteUrl GetInviteUrl() => new(invite.ToString());

        [Pure]
        public InviteCode GetInviteCode() => new(invite.Code);
    }
}
