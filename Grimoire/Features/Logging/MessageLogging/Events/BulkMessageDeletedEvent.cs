// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.LogCleanup.Commands;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed class BulkMessageDeletedEvent
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<MessagesBulkDeletedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessagesBulkDeletedEventArgs args)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (args.Guild is null)
                return;
            var response = await this._mediator.Send(
                new Request { Ids = args.Messages.Select(x => x.Id).ToArray(), GuildId = args.Guild.Id });
            if (!response.Success || !response.Messages.Any())
                return;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Bulk Message Delete")
                .WithDescription($"**Message Count:** {response.Messages.Count()}\n" +
                                 $"**Channel:** {ChannelExtensions.Mention(response.Messages.First().ChannelId)}\n" +
                                 "Full message dump attached.")
                .WithColor(GrimoireColor.Red);


            var message = await sender.SendMessageToLoggingChannel(response.BulkDeleteLogChannelId,
                async message => message
                    .AddEmbed(embed)
                    .AddFile($"{DateTime.UtcNow:r}.txt",
                        await BuildBulkMessageLogFile(response.Messages, args.Guild)));
            if (message is null)
                return;
            await this._mediator.Send(new AddLogMessage.Command
            {
                MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id
            });
        }

        private static async Task<MemoryStream> BuildBulkMessageLogFile(IEnumerable<MessageDto> messages,
            DiscordGuild guild)
        {
            var stringBuilder = new StringBuilder();
            foreach (var messageDto in messages)
            {
                var author = await guild.GetMemberAsync(messageDto.UserId);
                stringBuilder.AppendFormat(
                        "Author: {0} ({1})\n" +
                        "Id: {2}\n" +
                        "Content: {3}\n" +
                        (messageDto.Attachments.Any() ? "Attachments: {4}\n" : string.Empty),
                        author.GetUsernameWithDiscriminator(),
                        messageDto.UserId,
                        messageDto.MessageId,
                        messageDto.MessageContent,
                        string.Join("\n", messageDto.Attachments.Select(x => x.FileName)))
                    .AppendLine();
            }

            var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            await writer.WriteAsync(stringBuilder);
            await writer.FlushAsync();
            memoryStream.Position = 0;
            return memoryStream;
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong[] Ids { get; init; } = [];
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var messages = await this._grimoireDbContext.Messages
                .AsNoTracking()
                .WhereIdsAre(command.Ids)
                .WhereMessageLoggingIsEnabled()
                .Select(message => new
                    {
                        Message = new MessageDto
                        {
                            MessageId = message.Id,
                            UserId = message.UserId,
                            MessageContent = message.MessageHistory
                                .OrderByDescending(messageHistory => messageHistory.TimeStamp)
                                .First(messageHistory => messageHistory.Action != MessageAction.Deleted)
                                .MessageContent,
                            Attachments = message.Attachments
                                .Select(x => new AttachmentDto { Id = x.Id, FileName = x.FileName }),
                            ChannelId = message.ChannelId
                        },
                        BulkDeleteLogId = message.Guild.MessageLogSettings.BulkDeleteChannelLogId
                    }
                ).ToArrayAsync(cancellationToken);
            if (messages.Length == 0)
                return new Response { Success = false };

            var messageHistory = messages.Select(x =>
                new MessageHistory
                {
                    MessageId = x.Message.MessageId, Action = MessageAction.Deleted, GuildId = command.GuildId
                });

            await this._grimoireDbContext.MessageHistory.AddRangeAsync(messageHistory, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                BulkDeleteLogChannelId = messages.First().BulkDeleteLogId,
                Messages = messages.Select(x => x.Message),
                Success = true
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public IEnumerable<MessageDto> Messages { get; init; } = [];
        public ulong? BulkDeleteLogChannelId { get; init; }
        public bool Success { get; init; }
    }
}
