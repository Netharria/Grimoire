// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Logging.Commands;
using Grimoire.Core.Features.Logging.Queries;
using Grimoire.Discord.Enums;

namespace Grimoire.Discord.LoggingModule;

[SlashCommandGroup("Log", "View or change the settings of the Logging Modules.")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
public class LogSettingsCommands : ApplicationCommandModule
{
    [SlashRequireModuleEnabled(Module.UserLog)]
    [SlashCommandGroup("User", "View or change the User Log Module Settings.")]
    public class UserLogSettingsCommands(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("View", "View the current settings for the User Log module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetUserLogSettingsQuery{ GuildId = ctx.Guild.Id });
            var JoinChannelLog =
                response.JoinChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.JoinChannelLog.Value).Mention;
            var LeaveChannelLog  =
                response.LeaveChannelLog  is null ?
                "None" :
                ctx.Guild.GetChannel(response.LeaveChannelLog.Value).Mention;
            var UsernameChannelLog =
                response.UsernameChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.UsernameChannelLog.Value).Mention;
            var NicknameChannelLog =
                response.NicknameChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.NicknameChannelLog.Value).Mention;
            var AvatarChannelLog =
                response.AvatarChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.AvatarChannelLog.Value).Mention;
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Join Log:** {JoinChannelLog}\n" +
                $"**Leave Log:** {LeaveChannelLog}\n" +
                $"**Username Log:** {UsernameChannelLog}\n" +
                $"**Nickname Log:** {NicknameChannelLog}\n" +
                $"**Avatar Log:** {AvatarChannelLog}\n");
        }

        [SlashCommand("Set", "Set a User Log setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Choice("Joined Server Log", 0)]
            [Choice("Left Server Log", 1)]
            [Choice("Username Change Log", 2)]
            [Choice("Nickname Change Log", 3)]
            [Choice("Avatar Change Log", 4)]
            [Option("Setting", "The Setting to change.")] long loggingSetting,
            [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
            [Option("Value", "The channel to change the log to.")] DiscordChannel? channel = null)
        {
            var logSetting = (UserLogSetting)loggingSetting;
            channel = ctx.GetChannelOptionAsync(option, channel);
            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
                if (!permissions.HasPermission(Permissions.SendMessages))
                    throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
            }
            var response = await this._mediator.Send(new SetUserLogSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                UserLogSetting = logSetting,
                ChannelId = channel?.Id
            });
            if (option is ChannelOption.Off)
            {
                await ctx.ReplyAsync(message: $"Disabled {logSetting.GetName()}");
                await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} disabled {logSetting.GetName()}.");
            }
            await ctx.ReplyAsync(message: $"Updated {logSetting.GetName()} to {channel?.Mention}", ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated {logSetting.GetName()} to {channel?.Mention}.");
        }
    }

    [SlashCommandGroup("Message", "View or change the Message Log Module Settings.")]
    [SlashRequireModuleEnabled(Module.MessageLog)]
    public class MessageLogSettingsCommands(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("View", "View the current settings for the Message Log Module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetMessageLogSettingsQuery{ GuildId = ctx.Guild.Id });
            var DeleteChannelLog =
                response.DeleteChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.DeleteChannelLog.Value).Mention;
            var BulkDeleteChannelLog =
                response.BulkDeleteChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.BulkDeleteChannelLog.Value).Mention;
            var EditChannelLog =
                response.EditChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.EditChannelLog.Value).Mention;
            await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Delete Log:** {DeleteChannelLog}\n" +
                $"**Bulk Delete Log:** {BulkDeleteChannelLog}\n" +
                $"**Edit Log:** {EditChannelLog}\n");
        }

        [SlashCommand("Set", "Set a Message Log setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Choice("Delete Message Log", 0)]
            [Choice("Bulk Delete Message Log", 1)]
            [Choice("Edit Message Log", 2)]
            [Option("Setting", "The Setting to change.")] long loggingSetting,
            [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
            [Option("Value", "The channel to change the log setting to.")] DiscordChannel? channel = null)
        {
            var logSetting = (MessageLogSetting)loggingSetting;
            channel = ctx.GetChannelOptionAsync(option, channel);
            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
                if (!permissions.HasPermission(Permissions.SendMessages))
                    throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
            }

            var response = await this._mediator.Send(new SetMessageLogSettingsCommand
            {
                GuildId = ctx.Guild.Id,
                MessageLogSetting = logSetting,
                ChannelId = channel?.Id
            });

            if (option is ChannelOption.Off)
            {
                await ctx.ReplyAsync(message: $"Disabled {logSetting.GetName()}");
                await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} disabled {logSetting.GetName()}.");
            }
            await ctx.ReplyAsync(message: $"Updated {logSetting.GetName()} to {channel?.Mention}", ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated {logSetting.GetName()} to {channel?.Mention}.");
        }
    }
}
