// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Settings.Domain;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("ViewOverrides")]
        [Description("View the currently Configured log overrides")]
        public async Task ViewOverrides(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var channelOverrideString = new StringBuilder();

            await foreach (var channelOverride in this._settingsModule.GetAllOverriddenChannels(ctx.Guild.Id))
            {
                var channel = ctx.Guild.Channels.GetValueOrDefault(channelOverride.ChannelId)
                              ?? ctx.Guild.Threads.GetValueOrDefault(channelOverride.ChannelId);
                if (channel is null)
                    continue;

                channelOverrideString.Append(channel.Mention)
                    .Append(channelOverride.ChannelOption switch
                    {
                        MessageLogOverrideOption.AlwaysLog => " - Always Log",
                        MessageLogOverrideOption.NeverLog => " - Never Log",
                        _ => " - Inherit/Default"
                    }).AppendLine();
            }

            await ctx.EditReplyAsync(GrimoireColor.Purple, title: "Channel Override Settings",
                message: channelOverrideString.ToString());
        }
    }
}
