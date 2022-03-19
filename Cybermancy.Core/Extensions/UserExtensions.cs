// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class UserExtensions
    {
        public static GuildUser? GetGuildUser(this User user, ulong guildId) =>
            user.GuildMemberProfiles.FirstOrDefault(x => x.GuildId == guildId);
        public static string Mention(this User user) => $"<@!{user.Id}>";

        public static string Mention(this GuildUser user) => $"<@!{user.Id}>";
        public static string Mention(ulong id) => $"<@!{id}>";
    }
}
