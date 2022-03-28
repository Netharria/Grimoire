// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Attributes;
using Cybermancy.Core.Features.Logging.Commands.DeleteOldLogMessages;
using Cybermancy.Core.Features.Logging.Commands.MessageLoggingCommands.DeleteOldMessages;
using Cybermancy.Core.Features.Logging.Queries.GetOldLogMessages;
using Cybermancy.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using MediatR;

namespace Cybermancy.LoggingModule
{
    public static class LoggingBackgroundTasks
    {
        [RepeatTask(minutes:1)]
        public static async Task CleanupOldMessagesAsync()
        {
            Console.WriteLine("Deleting Old Messages");
            using var scope = ServiceActivator.GetScope();
            var mediator = (IMediator?)scope.ServiceProvider.GetService(typeof(IMediator));
            if (mediator is null) throw new ArgumentNullException(nameof(mediator));

            var discordClient = (DiscordClient?)scope.ServiceProvider.GetService(typeof(DiscordClient));
            if (discordClient is null) throw new ArgumentNullException(nameof(discordClient));

            var oldLogMessages = await mediator.Send(new GetOldLogMessagesQuery());


            var result = await oldLogMessages.Channels
                .ToAsyncEnumerable()
                .SelectAwait(async channel =>
                    new {
                        DiscordChannel = await GetChannelAsync(discordClient, channel.ChannelId),
                        DatabaseChannel = channel
                    })
                .SelectMany(channelInfo =>
                    channelInfo.DatabaseChannel.MessageIds
                    .ToAsyncEnumerable()
                    .SelectAwait(async messageId => await GetMessageAsync(channelInfo.DiscordChannel, messageId))
                    .SelectAwait(async channel => await DeleteMessageAsync(channel))
                    .Where(messageId => messageId != default)
                    ).ToArrayAsync();

            await mediator.Send(new DeleteOldMessagesCommand());
            if (result is ulong[] deletedMessageIds)
                await mediator.Send(new DeleteOldLogMessagesCommand { DeletedOldLogMessageIds = deletedMessageIds });
        }

        private static async Task<ulong> DeleteMessageAsync(DiscordMessage? message)
        {
            try
            {
                if(message == null)
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

        private static async Task<DiscordChannel?> GetChannelAsync(DiscordClient client, ulong channelId)
        {
            try
            {
                return await client.GetChannelAsync(channelId);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
