// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus;
using DSharpPlus.Entities;

namespace Cybermancy.Extensions
{
    public static class DiscordClientExtensions
    {
        public async static Task<DiscordChannel?> GetChannelOrDefaultAsync(this DiscordClient client, ulong channelId)
        {
            try
            {
                return await client.GetChannelAsync(channelId);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
