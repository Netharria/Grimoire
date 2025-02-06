// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerJoinedVoiceChannelEvent(IMediator mediator) : IEventHandler<VoiceStateUpdatedEventArgs>
{
    private readonly IMediator _mediator = mediator;

    public async Task HandleEventAsync(DiscordClient sender, VoiceStateUpdatedEventArgs args)
    {
        var response =
            await this._mediator.Send(new GetTracker.Query { UserId = args.User.Id, GuildId = args.Guild.Id });
        if (response is null) return;

        var loggingChannel = args.Guild.Channels.GetValueOrDefault(response.TrackerChannelId);

        if (loggingChannel is null) return;

        if (args.Before?.Channel is null && args.After?.Channel is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .AddField("User", args.User.Mention, true)
            .WithTimestamp(DateTime.UtcNow);

        if (args.Before?.Channel is null)
        {
            await loggingChannel.SendMessageAsync(
                embed
                    .WithAuthor("Joined Voice Channel")
                    .AddField("Channel", args.After.Channel?.Mention ?? "", true));
            return;
        }

        if (args.After?.Channel is null)
        {
            await loggingChannel.SendMessageAsync(
                embed
                    .WithAuthor("Left Voice Channel")
                    .AddField("Channel", args.Before.Channel.Mention, true));
            return;
        }

        if (args.Before.Channel != args.After.Channel)
            await loggingChannel.SendMessageAsync(
                embed
                    .WithAuthor("Moved Voice Channels")
                    .AddField("From", args.Before.Channel.Mention, true)
                    .AddField("To", args.After.Channel.Mention, true));
    }
}
