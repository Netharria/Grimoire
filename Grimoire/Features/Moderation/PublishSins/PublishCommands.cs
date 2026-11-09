// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Exceptions;
using Grimoire.DatabaseQueryHelpers;
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

    /// <summary>Publishes a ban or kick, which share everything but the sin type.</summary>
    private async Task PublishSinAsync(CommandContext ctx, SinId sinId, SinType sinType, PublishType publish)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .AsNoTracking()
            .Where(sin => sin.SinType == sinType && sin.Id == sinId && sin.GuildId == guild.GetGuildId())
            .Select(sin => new
            {
                sin.UserId,
                sin.SinOn,
                // ReSharper disable once AccessToDisposedClosure
                Reason = dbContext.SinReasonHistory
                    .Where(r => r.SinId == sin.Id)
                    .OrderByDescending(r => r.SetAt)
                    .Select(r => r.Reason.Value)
                    .FirstOrDefault() ?? string.Empty,
                PublishedMessageId = sin.PublishMessages
                    .Where(x => x.PublishType == publish)
                    .Select(x => (MessageId?)x.MessageId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, $"Could not find a {Noun(publish)} with that Sin Id.");
            return;
        }

        await this.PublishAsync(ctx, dbContext, sinId, publish, result.UserId, result.Reason, result.PublishedMessageId,
            result.SinOn);
    }

    /// <summary>Posts or updates the public entry, remembers it, and tells the moderator and the mod log.</summary>
    private async Task PublishAsync(CommandContext ctx, GrimoireDbContext dbContext, SinId sinId, PublishType publish,
        UserId userId, string reason, MessageId? publishedMessageId, DateTimeOffset actionDate)
    {
        var guild = ctx.RequiredGuild;
        var publicMessage = await this.SendPublicLogMessage(ctx, userId,
            await dbContext.UsernameHistory.GetDisplayNameAsync(userId), reason, publishedMessageId, actionDate, publish);

        if (publicMessage is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Red,
                $"Failed to publish {Noun(publish)} reason. Verify {guild.CurrentMember.Mention} has access to send messages in the public ban log channel.");
            return;
        }

        await SavePublishedMessageAsync(dbContext, sinId, publish, publishedMessageId, publicMessage.GetMessageId());

        await ctx.ReplyAsync(GrimoireColor.Green, $"Successfully published {Noun(publish)} : {sinId}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Mention} published {Noun(publish)} reason of sin {sinId}"
        });
    }

    private static string Noun(PublishType publish) => publish.ToString().ToLowerInvariant();

    private async Task<DiscordMessage?> SendPublicLogMessage(CommandContext ctx,
        UserId userId,
        string shownName,
        string reason,
        MessageId? publishedMessageId,
        DateTimeOffset actionDate,
        PublishType publish)
    {
        var guild = ctx.RequiredGuild;
        var banLogChannelId =
            await this._settingsModule.GetLogChannelSetting(GuildLogType.PublicModeration, guild.GetGuildId())
                .GetOrElse(() => null);

        if (banLogChannelId is null)
            return null;
        var banLogChannel = await ctx.Client.GetChannelOrDefaultAsync(banLogChannelId.Value);

        if (banLogChannel is null)
            return null;

        var embed = new DiscordEmbedBuilder()
            .WithTitle(publish.ToString())
            .WithDescription($"**Date:** {Formatter.Timestamp(actionDate, TimestampFormat.ShortDateTime)}\n" +
                             $"**User:** {shownName} ({userId})\n" +
                             $"**Reason:** {reason}")
            .WithColor(GrimoireColor.Purple)
            .Build();

        if (publishedMessageId is not null)
            try
            {
                var message = await banLogChannel.GetMessageAsync(publishedMessageId.Value);
                return await message.ModifyAsync(embed);
            }
            catch (NotFoundException ex)
            {
                LogPublishedMessageNotFound(this._logger, ex, publishedMessageId.Value);
            }

        return await banLogChannel.SendMessageAsync(embed);
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
