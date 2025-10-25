// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EntityFramework.Exceptions.Common;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.MessageLogging;

public sealed class UpdateMessageEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    TrackerLog trackerLog) : IEventHandler<MessageUpdatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task HandleEventAsync(DiscordClient sender, MessageUpdatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Guild is null
            || string.IsNullOrWhiteSpace(args.Message.Content))
            return;
        if (args.Message.Author?.Id == args.Guild.CurrentMember.Id)
            return;

        if (!await this._settingsModule.IsModuleEnabled(Module.MessageLog, args.Guild.Id))
            return;
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var message = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.GuildId == args.Guild.Id && message.Id == args.Message.Id)
            .Select(message => new
                {
                    MessageId = message.Id,
                    message.UserId,
                    message.MessageHistory
                        .OrderByDescending(messageHistory => messageHistory.TimeStamp)
                        .First(messageHistory => messageHistory.Action != MessageAction.Deleted)
                        .MessageContent,
                    Success = true,
                    OriginalUserId = (ulong?)message.ProxiedMessageLink!.OriginalMessage!.UserId,
                    message.ProxiedMessageLink.SystemId,
                    message.ProxiedMessageLink.MemberId
                }
            ).FirstOrDefaultAsync();
        if (message is null
            || message.MessageContent.Equals(args.Message.Content, StringComparison.CurrentCultureIgnoreCase))
            return;

        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomEmbed
        {
            TrackerId = args.Author.Id,
            GuildId = args.Guild.Id,
            TrackerIdType = TrackerIdType.UserId,
            Embed = new DiscordEmbedBuilder()
                .AddField("User", args.Author.Mention, true)
                .AddField("Channel", args.Channel.Mention, true)
                .AddField("Link", $"**[Jump URL]({args.Message.JumpLink})**", true)
                .WithFooter("Message Sent", args.Author.GetAvatarUrl(MediaFormat.Auto))
                .WithTimestamp(DateTime.UtcNow)
                .AddMessageTextToFields("Before", message.MessageContent)
                .AddMessageTextToFields("After", args.Message.Content)
        });

        await dbContext.MessageHistory.AddAsync(
            new MessageHistory
            {
                MessageId = message.MessageId,
                Action = MessageAction.Updated,
                GuildId = args.Guild.Id,
                MessageContent = args.Message.Content
            });
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (ReferenceConstraintException)
        {
            // ignored
        }

        var avatarUrl = await sender.GetUserAvatar(args.Author.Id, args.Guild);
        if (avatarUrl is null)
            return;


        var embeds = new List<DiscordEmbedBuilder>();
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
            var user = await sender.GetUserOrDefaultAsync(message.OriginalUserId.Value);
            if (user is not null)
                embed.AddField("Original Author", user.Mention, true);
            embed.AddField("System Id",
                    string.IsNullOrWhiteSpace(message.SystemId) ? "Private" : message.SystemId,
                    true)
                .AddField("Member Id",
                    string.IsNullOrWhiteSpace(message.MemberId) ? "Private" : message.MemberId,
                    true);
        }
        else
            embed.AddField("Author", args.Author.Mention, true);

        if (message.MessageContent.Length + args.Message.Content.Length >= 5000)
        {
            var afterEmbed = new DiscordEmbedBuilder(embed);
            embed.AddMessageTextToFields("Before", message.MessageContent);
            embeds.Add(embed);
            embeds.Add(afterEmbed.AddMessageTextToFields("After", args.Message.Content));
        }
        else
        {
            embed.AddMessageTextToFields("Before", message.MessageContent)
                .AddMessageTextToFields("After", args.Message.Content);
            embeds.Add(embed);
        }

        foreach (var embedToSend in embeds)
            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = args.Guild.Id, GuildLogType = GuildLogType.MessageEdited, Embed = embedToSend
            });
    }
}
