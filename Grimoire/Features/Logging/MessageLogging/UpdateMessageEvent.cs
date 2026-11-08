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
                Content = m.MessageHistory
                    .OfType<MessageHistoryContentEntry>()
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => (MessageContent?)h.Content)
                    .FirstOrDefault(),
                OriginalUserId = (UserId?)m.ProxiedMessageLink!.OriginalMessage!.UserId,
                SystemId = (PluralKitSystemId?)m.ProxiedMessageLink.SystemId,
                MemberId = (PluralKitMemberId?)m.ProxiedMessageLink.MemberId
            })
            .FirstOrDefaultAsync();

        if (message is null
            || (message.Content?.Equals(args.Message.GetMessageContent(),
                StringComparison.CurrentCultureIgnoreCase) ?? false))
            return;

        await dbContext.MessageHistory.AddAsync(new MessageEditedEntry
        {
            MessageId = message.MessageId,
            GuildId = args.Guild.GetGuildId(),
            Content = args.Message.GetMessageContent(),
            Timestamp = DateTimeOffset.UtcNow
        });
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (ReferenceConstraintException)
        {
            // ignored
        }

        // As in the delete log: the avatar is optional, and a proxied message shows the member behind it.
        var avatarUrl = await sender.GetUserAvatar(message.OriginalUserId ?? message.UserId, args.Guild);

        var embed = new DiscordEmbedBuilder()
            .WithDescription($"**[Jump Url]({args.Message.JumpLink})**")
            .AddField("Channel", args.Channel.Mention, true)
            .AddField("Message Id", args.Message.Id.ToString(), true)
            .WithAuthor($"Message edited in #{args.Channel.Name}")
            .WithTimestamp(DateTime.UtcNow)
            .WithColor(GrimoireColor.Yellow);
        if (avatarUrl is not null)
            embed.WithThumbnail(avatarUrl);

        embed.AddMessageAuthorFields(message.UserId, message.OriginalUserId, message.SystemId, message.MemberId);

        // Discord caps a message's embeds at 6,000 characters together, so a long edit sends Before and After
        // separately. The copy is taken first, so it carries only one of them.
        List<DiscordEmbedBuilder> embeds = message.Content.ToString()?.Length + args.Message.Content.Length >= 5000
            ?
            [
                new DiscordEmbedBuilder(embed).AddMessageTextToFields("Before", message.Content.ToString()),
                embed.AddMessageTextToFields("After", args.Message.Content)
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
