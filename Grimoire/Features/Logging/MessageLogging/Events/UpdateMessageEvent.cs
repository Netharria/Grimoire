// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.LogCleanup.Commands;

namespace Grimoire.Features.Logging.MessageLogging.Events;

public sealed class UpdateMessageEvent
{
    public sealed class EventHandler(IMediator mediator) : IEventHandler<MessageUpdatedEventArgs>
    {
        private readonly IMediator _mediator = mediator;

        public async Task HandleEventAsync(DiscordClient sender, MessageUpdatedEventArgs args)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (args.Guild is null
                || string.IsNullOrWhiteSpace(args.Message.Content))
                return;
            if(args.Message.Author?.Id == args.Guild.CurrentMember.Id)
                return;

            var response = await this._mediator.Send(
                new Command
                {
                    MessageId = args.Message.Id, GuildId = args.Guild.Id, MessageContent = args.Message.Content
                });

            if (!response.Success)
                return;

            var avatarUrl = await sender.GetUserAvatar(response.UserId, args.Guild);
            if (avatarUrl is null)
                return;


            var embeds = new List<DiscordEmbedBuilder>();
            var embed = new DiscordEmbedBuilder()
                .WithDescription($"**[Jump Url]({args.Message.JumpLink})**")
                .AddField("Channel", args.Channel.Mention, true)
                .AddField("Message Id", response.MessageId.ToString(), true)
                .WithAuthor($"Message edited in #{args.Channel.Name}")
                .WithTimestamp(DateTime.UtcNow)
                .WithColor(GrimoireColor.Yellow)
                .WithThumbnail(avatarUrl);

            if (response.OriginalUserId is not null)
                embed.AddField("Original Author", UserExtensions.Mention(response.OriginalUserId), true)
                    .AddField("System Id", string.IsNullOrWhiteSpace(response.SystemId) ? "Private" : response.SystemId,
                        true)
                    .AddField("Member Id", string.IsNullOrWhiteSpace(response.MemberId) ? "Private" : response.MemberId,
                        true);
            else
                embed.AddField("Author", UserExtensions.Mention(response.UserId), true);

            if (response.MessageContent.Length + args.Message.Content.Length >= 5000)
            {
                var afterEmbed = new DiscordEmbedBuilder(embed);
                embed.AddMessageTextToFields("Before", response.MessageContent);
                embeds.Add(embed);
                embeds.Add(afterEmbed.AddMessageTextToFields("After", args.Message.Content));
            }
            else
            {
                embed.AddMessageTextToFields("Before", response.MessageContent)
                    .AddMessageTextToFields("After", args.Message.Content);
                embeds.Add(embed);
            }

            foreach (var embedToSend in embeds)
            {
                var message = await sender.SendMessageToLoggingChannel(response.UpdateMessageLogChannelId,
                    message => message
                        .AddEmbed(embedToSend));
                if (message is null) return;
                await this._mediator.Send(new AddLogMessage.Command
                {
                    MessageId = message.Id, ChannelId = message.ChannelId, GuildId = args.Guild.Id
                });
            }
        }
    }

    public sealed record Command : IRequest<Response>
    {
        public required ulong MessageId { get; init; }
        public required ulong GuildId { get; init; }
        public string MessageContent { get; init; } = string.Empty;
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var message = await dbContext.Messages
                .AsNoTracking()
                .WhereMessageLoggingIsEnabled()
                .WhereIdIs(command.MessageId)
                .Select(message => new Response
                    {
                        UpdateMessageLogChannelId = message.Guild.MessageLogSettings.EditChannelLogId,
                        MessageId = message.Id,
                        UserId = message.UserId,
                        MessageContent = message.MessageHistory
                            .OrderByDescending(messageHistory => messageHistory.TimeStamp)
                            .First(messageHistory => messageHistory.Action != MessageAction.Deleted)
                            .MessageContent,
                        Success = true,
                        OriginalUserId = message.ProxiedMessageLink.OriginalMessage.UserId,
                        SystemId = message.ProxiedMessageLink.SystemId,
                        MemberId = message.ProxiedMessageLink.MemberId
                    }
                ).FirstOrDefaultAsync(cancellationToken);
            if (message is null
                || message.MessageContent.Equals(command.MessageContent, StringComparison.CurrentCultureIgnoreCase))
                return new Response { Success = false };

            await dbContext.MessageHistory.AddAsync(
                new MessageHistory
                {
                    MessageId = message.MessageId,
                    Action = MessageAction.Updated,
                    GuildId = command.GuildId,
                    MessageContent = command.MessageContent
                }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return message;
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong MessageId { get; init; }
        public ulong? UpdateMessageLogChannelId { get; init; }
        public string MessageContent { get; init; } = string.Empty;
        public ulong UserId { get; init; }
        public bool Success { get; init; }
        public ulong? OriginalUserId { get; init; }
        public string? SystemId { get; init; }
        public string? MemberId { get; init; }
    }
}
