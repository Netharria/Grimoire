// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using ChannelExtensions = Grimoire.Extensions.ChannelExtensions;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed class BulkMessageDeletedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog)
    : IEventHandler<MessagesBulkDeletedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, MessagesBulkDeletedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.Id))
            return;
        var messageIds = args.Messages.Select(x => x.Id).ToHashSet();
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var messages = await dbContext.MessageHistory
            .AsNoTracking()
            .Where(history => history.GuildId == args.Guild.Id)
            .Where(history => messageIds.Contains(history.MessageId))
            .GroupBy(history => new { history.MessageId, history.GuildId })
            .Select(historyGroup => new MessageDto
                {
                    MessageId = historyGroup.Key.MessageId,
                    UserId = dbContext.Messages
                        .Where(attachment => attachment.Id == historyGroup.Key.MessageId)
                        .Select(attachment => attachment.UserId)
                        .FirstOrDefault(),
                    MessageContent = historyGroup.MaxBy(history => history.TimeStamp)!.MessageContent,
                    Attachments = dbContext.Attachments
                        .Where(attachment => attachment.MessageId == historyGroup.Key.MessageId)
                        .Select(x =>
                            new AttachmentDto { Id = x.Id, FileName = x.FileName })
                }
            ).ToArrayAsync();
        if (messages.Length == 0)
            return;

        var messageHistory = messages.Select(x =>
            new MessageHistory
            {
                MessageId = x.MessageId,
                Action = MessageAction.Deleted,
                GuildId = args.Guild.Id,
                MessageContent = x.MessageContent
            });

        await dbContext.MessageHistory.AddRangeAsync(messageHistory);
        await dbContext.SaveChangesAsync();

        var embed = new DiscordEmbedBuilder()
            .WithTitle("Bulk Message Delete")
            .WithDescription($"**Message Count:** {messages.Length}\n" +
                             $"**Channel:** {ChannelExtensions.Mention(args.Channel.Id)}\n" +
                             "Full message dump attached.")
            .WithColor(GrimoireColor.Red);


        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.BulkMessageDeleted,
                Message = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddFile($"{DateTime.UtcNow:r}.txt",
                        await BuildBulkMessageLogFile(messages, args.Guild))
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

    private sealed record MessageDto
    {
        public required ulong UserId { get; init; }
        public required ulong MessageId { get; init; }
        public required string MessageContent { get; init; }
        public required IEnumerable<AttachmentDto> Attachments { get; init; }
    }
}
