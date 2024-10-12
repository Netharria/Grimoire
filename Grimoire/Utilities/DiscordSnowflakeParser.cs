// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Grimoire.Utilities;

public static partial class DiscordSnowflakeParser
{

    [GeneratedRegex(@"(\d{17,21})", RegexOptions.Compiled, 1000)]
    public static partial Regex MatchSnowflake();

    public static async ValueTask<Dictionary<string, string[]>> ParseStringIntoIdsAndGroupByTypeAsync(this InteractionContext ctx, string value) =>
        await MatchSnowflake().Matches(value)
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
                if (user is not null) return "User";
                return "Invalid";
            })
            .ToDictionaryAwaitAsync(k => ValueTask.FromResult(k.Key), async v => await v.ToArrayAsync());
}
