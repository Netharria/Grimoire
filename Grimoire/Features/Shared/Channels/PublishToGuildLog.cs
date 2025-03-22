// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Channels;

public class PublishToGuildLog
{
    public ulong? LogChannelId { get; init; }
    public string Title { get; init; } = string.Empty;
    public required string Description { get; init; }
    public string Footer { get; init; } = string.Empty;
    public DateTimeOffset? Timestamp { get; init; }
    public DiscordColor? Color { get; init; }

}

public sealed partial class PublishToGuildLogProcessor(Channel<PublishToGuildLog> channel, DiscordClient discordClient, ILogger<PublishToGuildLogProcessor> logger) : BackgroundService
{
    private readonly Channel<PublishToGuildLog> _channel = channel;
    private readonly DiscordClient _discordClient = discordClient;
    private readonly ILogger<PublishToGuildLogProcessor> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            try
            {
                var result = await this._channel.Reader.ReadAsync(stoppingToken);

                if (result.LogChannelId is null)
                    continue;
                var channel = await this._discordClient.GetChannelOrDefaultAsync(result.LogChannelId.Value);
                if (channel is null)
                    continue;
                var embed = new DiscordEmbedBuilder()
                    .WithAuthor(result.Title)
                    .WithDescription(result.Description)
                    .WithFooter(result.Footer)
                    .WithTimestamp(result.Timestamp ?? DateTimeOffset.UtcNow)
                    .WithColor(result.Color ?? GrimoireColor.Purple)
                    .Build();
                await DiscordRetryPolicy.RetryDiscordCall(async _ =>
                    await channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed)), stoppingToken);
            }
            catch (Exception e)
            {
                LogError(this._logger, e, e.Message);
            }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred while processing the log message. Message: ({message})")]
    static partial void LogError(ILogger logger, Exception e, string message);
}
