// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed class UpdateMessageEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog) : IEventHandler<MessageUpdatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, MessageUpdatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null
            || string.IsNullOrWhiteSpace(args.Message.Content))
            return;
        if (args.Message.Author?.Id == args.Guild.CurrentMember.Id)
            return;

        if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var message = await dbContext.Messages
            .AsNoTracking()
            .Where(m => m.GuildId == args.Guild.GetGuildId() && m.Id == args.Message.GetMessageId())
            .Select(m => new
            {
                MessageId = m.Id,
                m.UserId,
                // ReSharper disable once AccessToDisposedClosure
                Content = dbContext.MessageHistory
                    .OfType<MessageHistoryContentEntry>()
                    .Where(h => h.MessageId == m.Id)
                    .OrderByDescending(h => h.TimeStamp)
                    .Select(h => (MessageContent?)h.Content)
                    .FirstOrDefault(),
                OriginalUserId = (UserId?)m.ProxiedMessageLink!.OriginalMessage!.UserId,
                m.ProxiedMessageLink.SystemId,
                m.ProxiedMessageLink.MemberId
            })
            .FirstOrDefaultAsync();

        if (message is null
            || MessageContent.Equals(message.Content, args.Message.GetMessageContent(),
                StringComparison.CurrentCultureIgnoreCase))
            return;

        await dbContext.MessageHistory.AddAsync(new MessageEditedEntry
        {
            MessageId = message.MessageId,
            GuildId = args.Guild.GetGuildId(),
            Content = args.Message.GetMessageContent()
        });
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (ReferenceConstraintException)
        {
            // ignored
        }

        var avatarUrl = await sender.GetUserAvatar(args.Author.GetUserId(), args.Guild);
        if (avatarUrl is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .WithDescription($"**[Jump Url]({args.Message.JumpLink})**")
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", args.Message.Id.ToString(), true)
            .WithAuthor($"Message edited in #{args.Channel.Name}")
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Yellow)
            .WithThumbnail(avatarUrl);

        if (message.OriginalUserId is not null)
        {
            var user = await sender.GetUserOrDefaultAsync(message.OriginalUserId);
            if (user is not null)
                embed.AddField("Original Author", user.Mention, true);
            embed.AddField("System Id",
                    string.IsNullOrWhiteSpace(message.SystemId.Value) ? "Private" : message.SystemId.Value, true)
                .AddField("Member Id",
                    string.IsNullOrWhiteSpace(message.MemberId.Value) ? "Private" : message.MemberId.Value, true);
        }
        else
            embed.AddField("Author", args.Author.Mention, true);

        List<DiscordEmbedBuilder> embeds = message.Content.ToString()?.Length + args.Message.Content.Length >= 5000
            ?
            [
                embed.AddMessageTextToFields("Before", message.Content.ToString()),
                new DiscordEmbedBuilder(embed).AddMessageTextToFields("After", args.Message.Content)
            ]
            :
            [
                embed.AddMessageTextToFields("Before", message.Content.ToString())
                    .AddMessageTextToFields("After", args.Message.Content)
            ];

        foreach (var embedToSend in embeds)
            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.MessageEdited, Embed = embedToSend
            });
    }
}
