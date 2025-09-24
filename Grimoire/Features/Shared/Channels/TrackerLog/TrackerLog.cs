// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Channel = System.Threading.Channels.Channel;

namespace Grimoire.Features.Shared.Channels.TrackerLog;

public sealed partial class TrackerLog(
    DiscordClient discordClient,
    ILogger<TrackerLog> logger,
    SettingsModule settingsModule)
    : BackgroundService
{
    private readonly Channel<TrackerMessageBase> _channel =
        Channel.CreateUnbounded<TrackerMessageBase>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

    private readonly DiscordClient _discordClient = discordClient;
    private readonly ILogger<TrackerLog> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await this._channel.Reader.WaitToReadAsync(stoppingToken))
            try
            {
                var result = await this._channel.Reader.ReadAsync(stoppingToken);

                var logChannelId = await GetLogChannelId(result, stoppingToken);

                if (logChannelId is null)
                    continue;

                var channel = await this._discordClient.GetChannelOrDefaultAsync(logChannelId.Value);

                if (channel is null)
                    continue;
                await DiscordRetryPolicy.RetryDiscordCall(async _ =>
                    await channel.SendMessageAsync(result.GetMessageBuilder()), stoppingToken);
            }
            catch (Exception e)
            {
                LogError(this._logger, e, e.Message);
            }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "An error occurred while processing the log message. Message: ({message})")]
    static partial void LogError(ILogger logger, Exception e, string message);

    private async Task<ulong?> GetLogChannelId(TrackerMessageBase trackerMessageBase, CancellationToken stoppingToken)
    {
        var guildSettings = await this._settingsModule.GetGuildSettings(trackerMessageBase.GuildId, stoppingToken);
        if (guildSettings is null)
            return null;
        if (!await this._settingsModule.IsModuleEnabled(
                Module.MessageLog,
                trackerMessageBase.GuildId,
                stoppingToken))
            return null;

        return trackerMessageBase.TrackerIdType switch
        {
            TrackerIdType.UserId => GetUsersTrackerChannel(trackerMessageBase.TrackerId, guildSettings),
            TrackerIdType.ChannelId => trackerMessageBase.TrackerId,
            _ => throw new ArgumentOutOfRangeException(nameof(trackerMessageBase.TrackerIdType),
                trackerMessageBase.TrackerIdType, "Unknown log type")
        };
    }

    private static ulong? GetUsersTrackerChannel(ulong userId, GuildSettings guildSettings)
        => guildSettings.Trackers.FirstOrDefault(x => x.UserId == userId)?.LogChannelId;


    public ValueTask SendTrackerMessageAsync(TrackerMessageBase logMessageMessage,
        CancellationToken cancellationToken = default)
        => this._channel.Writer.WriteAsync(logMessageMessage, cancellationToken);
}
