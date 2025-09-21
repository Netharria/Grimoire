// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Settings;
using ChannelExtensions = Grimoire.Extensions.ChannelExtensions;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed class BulkMessageDeletedEvent
{
    public sealed class EventHandler(IMediator mediator, GuildLog guildLog)
        : IEventHandler<MessagesBulkDeletedEventArgs>
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessagesBulkDeletedEventArgs args)
        {
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


            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
                {
                    GuildId = args.Guild.Id,
                    GuildLogType = GuildLogType.BulkMessageDeleted,
                    Message = new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddFile($"{DateTime.UtcNow:r}.txt",
                            await BuildBulkMessageLogFile(response.Messages, args.Guild))
                }
            );
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
                        author.Mention,
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
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly SettingsModule _settingsModule = settingsModule;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, command.GuildId, cancellationToken))
                return new Response { Success = false };
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var messages = await dbContext.Messages
                .AsNoTracking()
                .WhereIdsAre(command.Ids)
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
                        }
                    }
                ).ToArrayAsync(cancellationToken);
            if (messages.Length == 0)
                return new Response { Success = false };

            var messageHistory = messages.Select(x =>
                new MessageHistory
                {
                    MessageId = x.Message.MessageId, Action = MessageAction.Deleted, GuildId = command.GuildId
                });

            await dbContext.MessageHistory.AddRangeAsync(messageHistory, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response { Messages = messages.Select(x => x.Message), Success = true };
        }
    }

    public sealed record Response
    {
        public IEnumerable<MessageDto> Messages { get; init; } = [];
        public bool Success { get; init; }
    }
}
