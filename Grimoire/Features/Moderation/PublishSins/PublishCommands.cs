// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.PublishSins;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[Command("Publish")]
[Description("Publishes a ban or unban to the public ban log channel.")]
public sealed partial class PublishCommands(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    ILogger<PublishCommands> logger)

{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<PublishCommands> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    private async Task<DiscordMessage?> SendPublicLogMessage(CommandContext ctx,
        UserId userId,
        Username? username,
        string reason,
        MessageId? publishedMessageId,
        DateTimeOffset actionDate,
        PublishType publish)
    {
        var guild = ctx.Guild!;
        var banLogChannelId =
            await this._settingsModule.GetLogChannelSetting(GuildLogType.PublicModeration, guild.GetGuildId())
                .GetOrElse(() => null);

        if (banLogChannelId is null)
            return null;
        var banLogChannel = await ctx.Client.GetChannelOrDefaultAsync(banLogChannelId.Value);

        if (banLogChannel is null)
            return null;

        var shownName = username?.Value ?? UserExtensions.Mention(userId);

        if (publishedMessageId is not null)
            try
            {
                var message = await banLogChannel.GetMessageAsync(publishedMessageId.Value);
                return await message.ModifyAsync(new DiscordEmbedBuilder()
                    .WithTitle(publish.ToString())
                    .WithDescription(
                        $"**Date:** {Formatter.Timestamp(actionDate, TimestampFormat.ShortDateTime)}\n" +
                        $"**User:** {shownName} ({userId})\n" +
                        $"**Reason:** {reason}")
                    .WithColor(GrimoireColor.Purple).Build());
            }
            catch (NotFoundException ex)
            {
                LogPublishedMessageNotFound(this._logger, ex, publishedMessageId.Value);
            }

        return await banLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithTitle(publish.ToString())
            .WithDescription($"**Date:** {Formatter.Timestamp(actionDate, TimestampFormat.ShortDateTime)}\n" +
                             $"**User:** {shownName} ({userId})\n" +
                             $"**Reason:** {reason}")
            .WithColor(GrimoireColor.Purple));
    }

    /// <summary>Remembers the public post, so a replacement for a deleted post is the one edited next time.</summary>
    private static async Task SavePublishedMessageAsync(GrimoireDbContext dbContext, SinId sinId, PublishType publish,
        MessageId? storedMessageId, MessageId messageId)
    {
        if (storedMessageId == messageId)
            return;

        if (storedMessageId is not null)
        {
            await dbContext.PublishedMessages
                .Where(message => message.SinId == sinId && message.PublishType == publish)
                .ExecuteUpdateAsync(update => update.SetProperty(message => message.MessageId, messageId));
            return;
        }

        await dbContext.PublishedMessages.AddAsync(
            new PublishedMessage { MessageId = messageId, SinId = sinId, PublishType = publish });
        await dbContext.SaveChangesAsync();
    }

    [LoggerMessage(LogLevel.Warning, "Could not find published message {id}")]
    static partial void LogPublishedMessageNotFound(ILogger<PublishCommands> logger, Exception ex, MessageId? id);
}
