// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup.Commands;

namespace Grimoire.Extensions;

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

    public async static Task<DiscordMessage?> SendMessageToLoggingChannel(this DiscordClient client, ulong? logChannelId, DiscordMessageBuilder discordMessageBuilder)
    {
        if (logChannelId is not ulong loggingChannelId)
            return null;
        var channel = await client.GetChannelOrDefaultAsync(loggingChannelId);
        if (channel is not DiscordChannel loggingChannel)
            return null;
        return await loggingChannel.SendMessageAsync(discordMessageBuilder);

    }
}
