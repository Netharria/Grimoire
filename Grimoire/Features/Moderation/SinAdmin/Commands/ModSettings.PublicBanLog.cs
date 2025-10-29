// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("PublicBanLog")]
    [Description("Set the public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        CommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("Channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
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

        await this._settingsModule.SetLogChannelSetting(GuildLogType.BanLog, guild.Id, channel?.Id);

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? "Disabled the public ban log."
            : $"Updated the public ban log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the public ban log."
                : $"{ctx.User.Mention} updated the public ban log to {channel?.Mention}."
        });
    }
}
