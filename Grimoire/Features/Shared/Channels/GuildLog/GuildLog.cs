// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Channel = System.Threading.Channels.Channel;

namespace Grimoire.Features.Shared.Channels.GuildLog;

public sealed partial class GuildLog(
    DiscordClient discordClient,
    ILogger<GuildLog> logger,
    SettingsModule settingsModule,
    IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : BackgroundService
{
    private readonly Channel<GuildLogMessageBase> _channel =
        Channel.CreateUnbounded<GuildLogMessageBase>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    private readonly DiscordClient _discordClient = discordClient;
    private readonly ILogger<GuildLog> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (await this._channel.Reader.WaitToReadAsync(cancellationToken))
            try
            {
                var result = await this._channel.Reader.ReadAsync(cancellationToken);

                var logChannelId = await this._settingsModule.GetLogChannelSetting(
                    result.GuildLogType,
                    result.GuildId,
                    cancellationToken);

                if (logChannelId is null)
                    continue;

                var channel = await this._discordClient.GetChannelOrDefaultAsync(logChannelId.Value);

                if (channel is null)
                    continue;
                var message = await DiscordRetryPolicy.RetryDiscordCall(async _ =>
                    await channel.SendMessageAsync(result.GetMessageBuilder()), cancellationToken);
                if (ShouldPurgeMessageAfterInterval(result.GuildLogType))
                    await ScheduleMessagePurge(message.Id, channel.Id, result.GuildId, cancellationToken);
            }
            catch (Exception e)
            {
                LogError(this._logger, e, e.Message);
            }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred while processing the log message. Message: ({message})")]
    static partial void LogError(ILogger logger, Exception e, string message);

    private static bool ShouldPurgeMessageAfterInterval(GuildLogType guildLogType)
    {
        return guildLogType switch
        {
            GuildLogType.Moderation => false,
            GuildLogType.Leveling => false,
            GuildLogType.BulkMessageDeleted => true,
            GuildLogType.MessageEdited => true,
            GuildLogType.MessageDeleted => true,
            GuildLogType.UserJoined => false,
            GuildLogType.UserLeft => false,
            GuildLogType.AvatarUpdated => false,
            GuildLogType.NicknameUpdated => false,
            GuildLogType.UsernameUpdated => false,
            GuildLogType.BanLog => false,
            _ => throw new ArgumentOutOfRangeException(nameof(guildLogType), guildLogType, "Unknown log type")
        };
    }


    public ValueTask SendLogMessageAsync(GuildLogMessageBase logMessageMessage,
        CancellationToken cancellationToken = default)
        => this._channel.Writer.WriteAsync(logMessageMessage, cancellationToken);

    private async Task ScheduleMessagePurge(ulong messageId, ulong channelId, ulong guildId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var logMessage = new OldLogMessage { ChannelId = channelId, GuildId = guildId, Id = messageId };
        await dbContext.OldLogMessages.AddAsync(logMessage, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
