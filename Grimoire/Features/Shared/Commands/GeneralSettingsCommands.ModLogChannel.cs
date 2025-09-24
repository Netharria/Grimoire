// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("ModLogChannel")]
    [Description("Set the moderation log channel.")]
    public async Task SetAsync(
        SlashCommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("Channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        guildSettings.ModLogChannelId = channel?.Id;

        await this._settingsModule.UpdateGuildSettings(guildSettings);

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? $"{ctx.User.Mention} disabled the moderation log to {channel?.Mention}"
            : $"Updated the moderation log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the moderation log."
                : $"{ctx.User.Mention} updated the moderation log to {channel?.Mention}.",
            Color = GrimoireColor.Purple
        });
    }
}
