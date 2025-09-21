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
    [Command("UserCommands")]
    [Description("Set the channel where some commands are visible for non moderators.")]
    public async Task SetUserCommandChannelAsync(
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

        var guild = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);
        guild.UserCommandChannelId = channel?.Id;
        await this._settingsModule.UpdateGuildSettings(guild);

        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: "Disabled the User Command Channel.");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description = $"{ctx.User.Mention} disabled the User Command Channel.",
                Color = GrimoireColor.Purple
            });
            return;
        }

        await ctx.EditReplyAsync(message: $"Updated the User Command Channel to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Mention} updated the User Command Channel to {channel?.Mention}.",
            Color = GrimoireColor.Purple
        });
    }
}
