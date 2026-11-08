// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("ViewOverrides")]
        [Description("View the currently Configured log overrides")]
        public async Task ViewOverrides(CommandContext ctx)
        {
            await ctx.DeferResponseAsync();

            var guild = ctx.Guild!;

            List<string> lines = [];
            await foreach (var channelOverride in this._settingsModule.GetAllOverriddenChannels(guild.GetGuildId()))
                if (await ctx.Client.GetChannelOrDefaultAsync(channelOverride.ChannelId) is { } channel)
                    // GetAllOverriddenChannels leaves out Inherit, so every override is one of these two.
                    lines.Add(channel.Mention + (channelOverride.ChannelOption == MessageLogOverrideOption.AlwaysLog
                        ? " - Always Log"
                        : " - Never Log"));

            await ctx.ReplyPagedAsync(
                new DiscordEmbedBuilder().WithColor(GrimoireColor.Purple).WithAuthor("Channel Override Settings"), lines);
        }
    }
}
