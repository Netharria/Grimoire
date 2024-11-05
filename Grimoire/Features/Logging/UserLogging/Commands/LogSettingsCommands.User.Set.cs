// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class User
    {
        [SlashCommand("Set", "Set a User Log setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Choice("Joined Server Log", 0)]
            [Choice("Left Server Log", 1)]
            [Choice("Username Change Log", 2)]
            [Choice("Nickname Change Log", 3)]
            [Choice("Avatar Change Log", 4)]
            [Option("Setting", "The Setting to change.")]
            long loggingSetting,
            [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Option("Value", "The channel to change the log to.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferAsync();
            var logSetting = (SetUserLogSettings.UserLogSetting)loggingSetting;
            channel = ctx.GetChannelOptionAsync(option, channel);
            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
                if (!permissions.HasPermission(DiscordPermissions.SendMessages))
                    throw new AnticipatedException(
                        $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
            }

            var response = await this._mediator.Send(new SetUserLogSettings.Command
            {
                GuildId = ctx.Guild.Id, UserLogSetting = logSetting, ChannelId = channel?.Id
            });
            if (option is ChannelOption.Off)
            {
                await ctx.EditReplyAsync(message: $"Disabled {logSetting.GetName()}");
                await ctx.SendLogAsync(response, GrimoireColor.Purple,
                    message: $"{ctx.User.Mention} disabled {logSetting.GetName()}.");
                return;
            }

            await ctx.EditReplyAsync(message: $"Updated {logSetting.GetName()} to {channel?.Mention}");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                message: $"{ctx.User.Mention} updated {logSetting.GetName()} to {channel?.Mention}.");
        }
    }
}

public sealed class SetUserLogSettings
{
    public enum UserLogSetting
    {
        JoinLog,
        LeaveLog,
        UsernameLog,
        NicknameLog,
        AvatarLog
    }

    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public UserLogSetting UserLogSetting { get; init; }
        public ulong? ChannelId { get; init; }
    }


    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var userSettings = await dbContext.GuildUserLogSettings
                .Where(x => x.GuildId == command.GuildId)
                .Select(x => new { UserSettings = x, x.Guild.ModChannelLog }).FirstOrDefaultAsync(cancellationToken);
            if (userSettings == null) throw new AnticipatedException("Could not find user log settings.");
            switch (command.UserLogSetting)
            {
                case UserLogSetting.JoinLog:
                    userSettings.UserSettings.JoinChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.LeaveLog:
                    userSettings.UserSettings.LeaveChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.UsernameLog:
                    userSettings.UserSettings.UsernameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.NicknameLog:
                    userSettings.UserSettings.NicknameChannelLogId = command.ChannelId;
                    break;
                case UserLogSetting.AvatarLog:
                    userSettings.UserSettings.AvatarChannelLogId = command.ChannelId;
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = userSettings.ModChannelLog };
        }
    }
}
