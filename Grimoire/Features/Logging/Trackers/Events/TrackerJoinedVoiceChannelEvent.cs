// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.TrackerLog;

namespace Grimoire.Features.Logging.Trackers.Events;

internal sealed class TrackerJoinedVoiceChannelEvent(TrackerLog trackerLog) : IEventHandler<VoiceStateUpdatedEventArgs>
{
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task HandleEventAsync(DiscordClient sender, VoiceStateUpdatedEventArgs args)
    {

        if (args.Before?.Channel is null && args.After?.Channel is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .AddField("User", args.User.Mention, true)
            .WithTimestamp(DateTime.UtcNow);

        if (args.Before?.Channel is null)
        {
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = args.Guild.Id,
                    TrackerId = args.User.Id,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor()
                        .AddField("Channel", args.After.Channel?.Mention ?? "", true)
                });
            return;
        }

        if (args.After?.Channel is null)
        {
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = args.Guild.Id,
                    TrackerId = args.User.Id,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor("Left Voice Channel")
                        .AddField("Channel", args.Before.Channel.Mention, true)
                });
            return;
        }

        if (args.Before.Channel != args.After.Channel)
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = args.Guild.Id,
                    TrackerId = args.User.Id,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor("Moved Voice Channels")
                        .AddField("From", args.Before.Channel.Mention, true)
                        .AddField("To", args.After.Channel.Mention, true)
                });
    }
}
