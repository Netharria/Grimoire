// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("PublicBanLog")]
    [Description("Set the public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        CommandContext ctx,
        [Parameter("option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        await ChannelSettingCommand.SetAsync(ctx, this._guildLog, option, channel, "the public ban log",
            channelId => this._settingsModule.SetLogChannelSetting(GuildLogType.PublicModeration,
                ctx.Guild!.GetGuildId(), ctx.GetModeratorId(), channelId));
    }
}
