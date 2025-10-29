// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
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
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("Channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();


        var guild = ctx.Guild!;

        var channelOption = ctx.GetChannelOption(option, channel);

        if (channelOption.IsLeft)
        {
            await ctx.EditReplyAsync(DiscordColor.Red, $"");
            return;
        }

        channelOption.Match(
            success => channel = success,
            error => { });

        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
            {
                await ctx.EditReplyAsync(DiscordColor.Red, $"{guild.CurrentMember.Mention} don't have permission to send messages in that channel.");
                return;
            }
        }

        await this._settingsModule.SetLogChannelSetting(GuildLogType.Moderation, guild.Id, channel?.Id);

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? $"{ctx.User.Mention} disabled the moderation log to {channel?.Mention}"
            : $"Updated the moderation log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the moderation log."
                : $"{ctx.User.Mention} updated the moderation log to {channel?.Mention}.",
            Color = GrimoireColor.Purple
        });
    }
}
