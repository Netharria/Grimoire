// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed partial class AddMessageEvent
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<MessageCreatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (args.Guild is null
                || args.Message.MessageType is not DiscordMessageType.Default and not DiscordMessageType.Reply)
                return;

            await this._mediator.Send(new Command
            {
                Attachments = args.Message.Attachments
                    .Select(x =>
                        new AttachmentDto { Id = x.Id, FileName = string.IsNullOrEmpty(x.Url) ? "" : x.Url }).ToArray(),
                UserId = args.Author.Id,
                ChannelId = args.Channel.Id,
                MessageContent = args.Message.Content,
                MessageId = args.Message.Id,
                ReferencedMessageId = args.Message.ReferencedMessage?.Id,
                GuildId = args.Guild.Id,
                ParentChannelTree = args.Channel.BuildChannelTree()
            });
        }
    }


    public sealed record Command : IRequest
    {
        public required ulong MessageId { get; init; }
        public required ulong ChannelId { get; init; }
        public required string MessageContent { get; init; }
        public required ulong UserId { get; init; }
        public required AttachmentDto[] Attachments { get; init; }
        public ulong? ReferencedMessageId { get; init; }
        public required ulong GuildId { get; init; }
        public required IEnumerable<ulong> ParentChannelTree { get; init; }
    }

    public sealed partial class Handler(
        IDbContextFactory<GrimoireDbContext> dbContextFactoryFactoryFactory,
        ILogger<Handler> logger)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactoryFactoryFactory;
        private readonly ILogger<Handler> _logger = logger;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            try
            {
                await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
                var result = await dbContext.Guilds
                    .AsNoTracking()
                    .WhereIdIs(command.GuildId)
                    .Select(guild => new
                    {
                        guild.MessageLogSettings.ModuleEnabled,
                        ChannelExists = guild.Channels.Any(x => x.Id == command.ChannelId),
                        MemberExists = guild.Members.Any(x => x.UserId == command.UserId),
                        ChannelLogOverride = guild.MessageLogChannelOverrides
                            .Where(x => command.ParentChannelTree.Contains(x.ChannelId))
                            .Select(x => new { x.ChannelId, x.ChannelOption })
                            .ToArray()
                    }).FirstOrDefaultAsync(cancellationToken);
                if (result is null)
                    throw new KeyNotFoundException("Guild was not found in database.");
                if (!result.ModuleEnabled)
                    return;
                if (!result.ChannelExists)
                {
                    var channel = new Channel { GuildId = command.GuildId, Id = command.ChannelId };
                    await dbContext.Channels.AddAsync(channel, cancellationToken);
                }

                if (!result.MemberExists) await AddMissingMember(command, dbContext, cancellationToken);

                if (!ShouldLogMessage(command,
                        result.ChannelLogOverride.ToDictionary(k => k.ChannelId, v => v.ChannelOption)))
                    return;

                var message = new Message
                {
                    Id = command.MessageId,
                    UserId = command.UserId,
                    Attachments = command.Attachments
                        .Select(x =>
                            new Attachment { Id = x.Id, MessageId = command.MessageId, FileName = x.FileName })
                        .ToArray(),
                    ChannelId = command.ChannelId,
                    ReferencedMessageId = command.ReferencedMessageId,
                    GuildId = command.GuildId,
                    MessageHistory =
                    [
                        new MessageHistory
                        {
                            MessageId = command.MessageId,
                            MessageContent = command.MessageContent,
                            GuildId = command.GuildId,
                            Action = MessageAction.Created
                        }
                    ]
                };
                await dbContext.Messages.AddAsync(message, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                LogOriginalMessageForDebugging(this._logger, command.MessageContent);
                throw;
            }
        }

        [LoggerMessage(LogLevel.Error,
            "Database through exception on message creation. This was the original message {message}")]
        private static partial void LogOriginalMessageForDebugging(ILogger logger, string message);

        private static async Task AddMissingMember(Command command, GrimoireDbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (!await dbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken))
                await dbContext.Users.AddAsync(new User { Id = command.UserId }, cancellationToken);
            await dbContext.Members.AddAsync(new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory =
                [
                    new XpHistory
                    {
                        UserId = command.UserId,
                        GuildId = command.GuildId,
                        Xp = 0,
                        Type = XpHistoryType.Created,
                        TimeOut = DateTime.UtcNow
                    }
                ]
            }, cancellationToken);
        }

        private static bool ShouldLogMessage(Command command, Dictionary<ulong, MessageLogOverrideOption> overrides)
        {
            foreach (var channel in command.ParentChannelTree)
            {
                if (!overrides.TryGetValue(channel, out var channelOverride))
                    continue;
                return channelOverride switch
                {
                    MessageLogOverrideOption.NeverLog => false,
                    MessageLogOverrideOption.AlwaysLog => true,
                    _ => true
                };
            }

            return true;
        }
    }
}
