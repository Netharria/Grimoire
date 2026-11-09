// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("ModLogChannel")]
    [Description("Set the moderation log channel.")]
    public async Task SetAsync(
        CommandContext ctx,
        [Parameter("option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();
        await ChannelSettingCommand.SetAsync(ctx, this._guildLog, option, channel, "the moderation log",
            channelId => this._settingsModule.SetLogChannelSetting(GuildLogType.Moderation, ctx.RequiredGuild.GetGuildId(),
                ctx.GetModeratorId(), channelId),
            isModerationLog: true);
    }
}
