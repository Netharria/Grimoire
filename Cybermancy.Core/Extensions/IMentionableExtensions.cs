// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.Extensions
{
    public static class IMentionableExtensions
    {
        public static string Mention(this IMentionable ignorable)
            => 
            ignorable switch
            {
                Member member => $"<@!{member.UserId}>",
                User user => $"<@!{user.Id}>",
                Role role => $"<@&{role.Id}>",
                Reward reward => $"<@&{reward.RoleId}>",
                Channel channel => $"<#{channel.Id}>",
                _ => throw new NotImplementedException(),
            };
        public static string Mention(this IXpIgnore ignorable)
            =>
            ignorable switch
            {
                Member member => $"<@!{member.UserId}>",
                Role role => $"<@&{role.Id}>",
                Channel channel => $"<#{channel.Id}>",
                _ => throw new NotImplementedException(),
            };
    }
}
