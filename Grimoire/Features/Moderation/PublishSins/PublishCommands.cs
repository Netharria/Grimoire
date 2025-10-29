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
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Optional = LanguageExt.Optional;

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

    private async Task<Either<Error, DiscordMessage>> SendPublicLogMessage(CommandContext ctx,
        ulong userId,
        string username,
        string reason,
        ulong? publishedMessageId,
        DateTimeOffset actionDate,
        PublishType publish)
    {
        var guild = ctx.Guild!;
        var banLogChannelId = await this._settingsModule.GetLogChannelSetting(GuildLogType.BanLog, guild.Id);

        if (banLogChannelId is null)
            return Error.New("The public ban log channel is not set up. Please set it up and try again.");
        var banLogChannel = await ctx.Client.GetChannelOrDefaultAsync(banLogChannelId.Value);

        if (banLogChannel is null)
            return Error.New("The public ban log channel is invalid. Please set it up and try again.");

        if (string.IsNullOrWhiteSpace(username))
        {
            var user = await ctx.Client.GetUserAsync(userId);
            username = user.Username;
        }

        if (publishedMessageId is not null)
            try
            {
                var message = await banLogChannel.GetMessageAsync(publishedMessageId.Value);
                return await message.ModifyAsync(new DiscordEmbedBuilder()
                    .WithTitle(publish.ToString())
                    .WithDescription(
                        $"**Date:** {Formatter.Timestamp(actionDate, TimestampFormat.ShortDateTime)}\n" +
                        $"**User:** {username} ({userId})\n" +
                        $"**Reason:** {reason}")
                    .WithColor(GrimoireColor.Purple).Build());
            }
            catch (NotFoundException ex)
            {
                LogPublishedMessageNotFound(this._logger, ex, publishedMessageId);
            }

        return await banLogChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithTitle(publish.ToString())
            .WithDescription($"**Date:** {Formatter.Timestamp(actionDate, TimestampFormat.ShortDateTime)}\n" +
                             $"**User:** {username} ({userId})\n" +
                             $"**Reason:** {reason}")
            .WithColor(GrimoireColor.Purple));
    }

    [LoggerMessage(LogLevel.Warning, "Could not find published message {id}")]
    static partial void LogPublishedMessageNotFound(ILogger<PublishCommands> logger, Exception ex, ulong? id);
}
