// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Logging.Commands.DeleteOldLogMessages;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages;
using Cybermancy.Core.Features.Logging.Queries.GetOldLogMessages;
using Cybermancy.Discord.Utilities;
using DSharpPlus;
using DSharpPlus.Entities;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting;

namespace Cybermancy.Discord.LoggingModule
{
    public class LoggingBackgroundTasks : INotificationHandler<TimedNotification>
    {
        private readonly IMediator _mediator;
        private readonly IDiscordClientService _discordClientService;

        public LoggingBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
        {
            this._mediator = mediator;
            this._discordClientService = discordClientService;
        }

        public async Task Handle(TimedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Time.Second % 5 != 0)
                return;
            Console.WriteLine("Deleting Old Messages");

            var oldLogMessages = await this._mediator.Send(new GetOldLogMessagesQuery(), cancellationToken);


            var result = await oldLogMessages.Channels
                .ToAsyncEnumerable()
                .SelectAwait(async channel =>
                    new
                    {
                        DiscordChannel = await this.GetChannelAsync(channel.ChannelId),
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

        private async Task<DiscordChannel?> GetChannelAsync(ulong channelId)
        {
            try
            {
                return await this._discordClientService.Client.GetChannelAsync(channelId);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
