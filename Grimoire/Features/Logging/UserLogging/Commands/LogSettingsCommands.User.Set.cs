// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;

// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class User
    {
        [Command("Set")]
        [Description("Set a User Log setting.")]
        public async Task SetAsync(
            SlashCommandContext ctx,
            [Parameter("Setting")]
            [Description("The setting to change.")]
            SetUserLogSettings.UserLogSetting logSetting,
            [Parameter("Option")]
            [Description("Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Parameter("Value")]
            [Description("The channel to change the log setting to.")]
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

            await this._mediator.Send(new SetUserLogSettings.Command
            {
                GuildId = ctx.Guild.Id, UserLogSetting = logSetting, ChannelId = channel?.Id
            });


            await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? $"Disabled {logSetting}"
            : $"Updated {logSetting} to {channel?.Mention}");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Description = option is ChannelOption.Off
                    ? $"{ctx.User.Mention} disabled {logSetting}."
                    : $"{ctx.User.Mention} updated {logSetting} to {channel?.Mention}.",
                Color = GrimoireColor.Purple
            });
        }
    }
}

public sealed class SetUserLogSettings
{
    public enum UserLogSetting
    {
        [ChoiceDisplayName("Joined Server Log")]JoinLog,
        [ChoiceDisplayName("Left Server Log")]LeaveLog,
        [ChoiceDisplayName("Username Change Log")]UsernameLog,
        [ChoiceDisplayName("Nickname Change Log")]NicknameLog,
        [ChoiceDisplayName("Avatar Change Log")]AvatarLog
    }

    public sealed record Command : IRequest
    {
        public ulong GuildId { get; init; }
        public UserLogSetting UserLogSetting { get; init; }
        public ulong? ChannelId { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var userSettings = await dbContext.GuildUserLogSettings
                .Where(x => x.GuildId == command.GuildId)
                .FirstOrDefaultAsync(cancellationToken);
            if (userSettings == null) throw new AnticipatedException("Could not find user log settings.");
            switch (command.UserLogSetting)
            {
                case UserLogSetting.JoinLog:
                    userSettings.JoinChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.LeaveLog:
                    userSettings.LeaveChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.UsernameLog:
                    userSettings.UsernameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.NicknameLog:
                    userSettings.NicknameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.AvatarLog:
                    userSettings.AvatarChannelLogId = command.ChannelId;
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
