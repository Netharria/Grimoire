// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Logging.Commands.DeleteOldLogMessages;
using Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages;
using Grimoire.Core.Features.Logging.Queries.GetOldLogMessages;

namespace Grimoire.Discord.LoggingModule
{
    public class LogBackgroundTasks : INotificationHandler<TimedNotification>
    {
        private readonly IMediator _mediator;
        private readonly IDiscordClientService _discordClientService;

        public LogBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
        {
            this._mediator = mediator;
            this._discordClientService = discordClientService;
        }

        public async ValueTask Handle(TimedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Time.Second % 60 != 0)
                return;

            var oldLogMessages = await this._mediator.Send(new GetOldLogMessagesQuery(), cancellationToken);


            var result = await oldLogMessages
                .ToAsyncEnumerable()
                .Select(channel =>
                    new
                    {
                        DiscordChannel = this.GetChannelAsync(channel.GuildId, channel.ChannelId),
                        DatabaseChannel = channel
                    })
                .SelectMany(channelInfo =>
                    channelInfo.DatabaseChannel.MessageIds
                    .ToAsyncEnumerable()
                    .SelectAwait(async messageId => await GetMessageAsync(channelInfo.DiscordChannel, messageId))
                    .SelectAwait(async channel => await DeleteMessageAsync(channel))
                    .Where(messageId => messageId != default)
                    ).ToArrayAsync(cancellationToken: cancellationToken);

            await this._mediator.Send(new DeleteOldMessagesCommand(), cancellationToken);
            if (result is ulong[] deletedMessageIds)
                await this._mediator.Send(new DeleteOldLogMessagesCommand { DeletedOldLogMessageIds = deletedMessageIds }, cancellationToken);
        }

        private static async Task<ulong> DeleteMessageAsync(DiscordMessage? message)
        {
            try
            {
                if (message is null)
                    return default;
                await message.DeleteAsync();
                return message.Id;
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static async Task<DiscordMessage?> GetMessageAsync(DiscordChannel? channel, ulong messageId)
        {
            try
            {
                if (channel is null)
                    return null;
                return await channel.GetMessageAsync(messageId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private DiscordChannel? GetChannelAsync(ulong guildId, ulong channelId)
        {
            try
            {
                return this._discordClientService.Client.Guilds.GetValueOrDefault(guildId)?.Channels.GetValueOrDefault(channelId);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
