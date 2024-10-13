// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.MessageLogging.Queries;
using Grimoire.Notifications;

namespace Grimoire.Features.MessageLogging;

internal sealed class TrackerNicknameEvent(DiscordClient clientService, IMediator mediator) : INotificationHandler<NicknameTrackerNotification>
{
    private readonly DiscordClient _clientService = clientService;
    private readonly IMediator _mediator = mediator;

    public async ValueTask Handle(NicknameTrackerNotification notification, CancellationToken cancellationToken)
    {
        var response = await this._mediator.Send(
            new GetTrackerQuery
            {
                UserId = notification.UserId,
                GuildId = notification.GuildId
            }
            , cancellationToken);

        if (response is null) return;
        if (!this._clientService.Guilds.TryGetValue(notification.GuildId, out var guild)) return;
        if (!guild.Channels.TryGetValue(response.TrackerChannelId, out var logChannel)) return;
        var embed = new DiscordEmbedBuilder()
                .WithAuthor("Nickname Updated")
                .AddField("User", UserExtensions.Mention(notification.UserId))
                .AddField("Before", string.IsNullOrWhiteSpace(notification.BeforeNickname)? "None" : notification.BeforeNickname, true)
                .AddField("After", string.IsNullOrWhiteSpace(notification.AfterNickname)? "None" : notification.AfterNickname, true)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint);
        await logChannel.SendMessageAsync(embed);
    }
}
