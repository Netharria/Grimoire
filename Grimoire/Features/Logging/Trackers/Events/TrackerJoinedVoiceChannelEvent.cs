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
        if ((args.Before.ChannelId is null
             && args.After.ChannelId is null)
            || args.GuildId is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .AddField("User", UserExtensions.Mention(new UserId(args.UserId)), true)
            .WithTimestamp(DateTime.UtcNow);

        if (args.Before.ChannelId is null)
        {
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = new GuildId(args.GuildId.Value),
                    TrackerId = args.UserId,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor()
                        .AddField("Channel", ChannelExtensions.Mention(args.After.ChannelId), true)
                });
            return;
        }

        if (args.After.ChannelId is null)
        {
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = new GuildId(args.GuildId.Value),
                    TrackerId = args.UserId,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor("Left Voice Channel")
                        .AddField("Channel", ChannelExtensions.Mention(args.Before.ChannelId), true)
                });
            return;
        }

        if (args.Before.ChannelId != args.After.ChannelId)
            await this._trackerLog.SendTrackerMessageAsync(
                new TrackerMessageCustomEmbed
                {
                    GuildId = new GuildId(args.GuildId.Value),
                    TrackerId = args.UserId,
                    TrackerIdType = TrackerIdType.UserId,
                    Embed = embed
                        .WithAuthor("Moved Voice Channels")
                        .AddField("From", ChannelExtensions.Mention(args.Before.ChannelId), true)
                        .AddField("To", ChannelExtensions.Mention(args.After.ChannelId), true)
                });
    }
}
