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
    public partial class Message
    {
        [SlashCommand("Set", "Set a Message Log setting.")]
        public async Task SetAsync(
            InteractionContext ctx,
            [Choice("Delete Message Log", 0)]
            [Choice("Bulk Delete Message Log", 1)]
            [Choice("Edit Message Log", 2)]
            [Option("Setting", "The Setting to change.")]
            long loggingSetting,
            [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")]
            ChannelOption option,
            [Option("Value", "The channel to change the log setting to.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferAsync();
            var logSetting = (SetMessageLogSettings.MessageLogSetting)loggingSetting;
            channel = ctx.GetChannelOptionAsync(option, channel);
            if (channel is not null)
            {
                var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
                if (!permissions.HasPermission(DiscordPermission.SendMessages))
                    throw new AnticipatedException(
                        $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
            }

            var response = await this._mediator.Send(new SetMessageLogSettings.Command
            {
                GuildId = ctx.Guild.Id, MessageLogSetting = logSetting, ChannelId = channel?.Id
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

public sealed class SetMessageLogSettings
{
    public enum MessageLogSetting
    {
        DeleteLog,
        BulkDeleteLog,
        EditLog
    }

    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public MessageLogSetting MessageLogSetting { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var messageSettings = await dbContext.GuildMessageLogSettings
                .Where(x => x.GuildId == command.GuildId)
                .Select(x => new { LogSettings = x, x.Guild.ModChannelLog }).FirstOrDefaultAsync(cancellationToken);
            if (messageSettings == null) throw new AnticipatedException("Could not find message log settings.");
            switch (command.MessageLogSetting)
            {
                case MessageLogSetting.DeleteLog:
                    messageSettings.LogSettings.DeleteChannelLogId = command.ChannelId;
                    break;
                case MessageLogSetting.BulkDeleteLog:
                    messageSettings.LogSettings.BulkDeleteChannelLogId = command.ChannelId;
                    break;
                case MessageLogSetting.EditLog:
                    messageSettings.LogSettings.EditChannelLogId = command.ChannelId;
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = messageSettings.ModChannelLog };
        }
    }
}
