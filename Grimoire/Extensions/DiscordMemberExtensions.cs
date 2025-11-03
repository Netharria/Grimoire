// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Diagnostics.Contracts;

namespace Grimoire.Extensions;

public static class DiscordMemberExtensions
{
    [Pure]
    public static UserId GetUserId(this DiscordMember member) => new (member.Id);
    [Pure]
    public static GuildId GetGuildId(this DiscordMember member) => new (member.Guild.Id);
    [Pure]
    public static Nickname GetNickname(this DiscordMember member) => new (member.Nickname);
    [Pure]
    public static AvatarFileName GetAvatarFileName(this DiscordMember member) => new (member.AvatarUrl);


    [Pure]
    public static AvatarFileName GetAvatarFileName(this DiscordMember member, MediaFormat mediaFormat, ushort imageSize = 1024)
        => new (member.GetGuildAvatarUrl(mediaFormat, imageSize));
}
