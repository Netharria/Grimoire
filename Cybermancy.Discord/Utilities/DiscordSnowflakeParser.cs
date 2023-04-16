// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Cybermancy.Discord.Utilities
{
    public static class DiscordSnowflakeParser
    {

        public static async ValueTask<Dictionary<string, string[]>> ParseStringIntoIdsAndGroupByTypeAsync(InteractionContext ctx, string value) =>
            await Regex.Matches(value, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1))
                .Where(x => x.Success)
                .Select(x => x.Value)
                .ToAsyncEnumerable()
                .GroupByAwait(async x =>
                {
                    if (!ulong.TryParse(x, out var id)) return "Invalid";
                    if (ctx.Guild.Members.ContainsKey(id)) return "User";
                    if (ctx.Guild.Roles.ContainsKey(id)) return "Role";
                    if (ctx.Guild.Channels.ContainsKey(id)) return "Channel";
                    var user = await ctx.Client.GetUserAsync(id);
                    if (user != null) return "User";
                    return "Invalid";
                })
                .ToDictionaryAwaitAsync(k => new ValueTask<string>(k.Key), async v => await v.ToArrayAsync());
    }
}
