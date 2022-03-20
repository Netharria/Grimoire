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
            var result = oldLogMessages.Channels
                    .AsParallel()
                    .SelectMany(channel =>
                        GetChannelAsync(discordClient, channel.ChannelId)
                        .ContinueWith(discordChannel => channel.MessageIds
                            .AsParallel()
                            .Select(messageId => GetMessageAsync(discordChannel.Result, messageId)
                                .ContinueWith(message =>
                                    DeleteMessageAsync(message.Result)
                                    .GetAwaiter().GetResult()
                                ).GetAwaiter().GetResult()
                            ).Where(x => x != default)
                        ).GetAwaiter().GetResult())
                    .ToArray();

            await mediator.Send(new DeleteOldMessagesCommand());
            await mediator.Send(new DeleteOldLogMessagesCommand { DeletedOldLogMessageIds = result });
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
