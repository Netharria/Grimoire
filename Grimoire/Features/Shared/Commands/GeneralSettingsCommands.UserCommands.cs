// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("UserCommands")]
    [Description("Set the channel where some commands are visible for non moderators.")]
    public async Task SetUserCommandChannelAsync(
        CommandContext ctx,
        [Parameter("option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();
        // Replies there are interaction responses, so the bot needn't be able to send in it.
        await ChannelSettingCommand.SetAsync(ctx, this._guildLog, option, channel, "the User Command Channel",
            channelId => this._settingsModule.SetUserCommandChannelSetting(ctx.Guild!.GetGuildId(),
                ctx.GetModeratorId(), channelId),
            botMustSend: false);
    }
}
