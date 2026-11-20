// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Documents;
using LanguageExt;

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

            var guild = ctx.RequiredGuild;

            // A failure gets the error reply, not an empty list, which would look like no overrides (spec 2.5, OQ-4).
            if (await this._settingsModule.GetAllOverriddenChannels(guild.GetGuildId())
                is not Fin<Map<ChannelId, MessageLogOverrideOption>>.Succ { Value: var overrides })
            {
                await ctx.SendErrorResponseAsync("Could not load the log overrides. Please try again later.");
                return;
            }

            List<string> lines = [];
            foreach (var (channelId, option) in overrides)
                if (await ctx.Client.GetChannelOrDefaultAsync(channelId) is { } channel)
                    lines.Add(channel.Mention + (option is MessageLogOverrideOption.AlwaysLog
                        ? " - Always Log"
                        : " - Never Log"));

            await ctx.ReplyPagedAsync(
                new DiscordEmbedBuilder().WithColor(GrimoireColor.Purple).WithAuthor("Channel Override Settings"), lines);
        }
    }
}
