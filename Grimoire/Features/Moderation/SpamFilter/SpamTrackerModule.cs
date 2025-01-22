// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Features.Moderation.SpamFilter;

public class SpamTrackerModule
{
    private readonly ConcurrentDictionary<DiscordMember, SpamTracker> _spamUsers = new();

    public CheckSpamResult CheckSpam(DiscordMessage message)
    {
        if (message.Author is not DiscordMember member)
            return new CheckSpamResult { IsSpam = false };

        if (member.Permissions.HasPermission(DiscordPermissions.ManageChannels) || member.IsOwner)
            return new CheckSpamResult { IsSpam = false };

        var spamTracker = this._spamUsers.GetOrAdd(member, new SpamTracker());

        if (message.Id == spamTracker.LastMessageId)
            return new CheckSpamResult { IsSpam = false };

        spamTracker.LastMessageId = message.Id;

        spamTracker.PointTotal -= (message.Timestamp - spamTracker.DateTimeOffset).TotalSeconds * 2;

        if (spamTracker.PointTotal < 0)
            spamTracker.PointTotal = 0;

        if (AddPressure(spamTracker, 10))
            return new CheckSpamResult { IsSpam = true, Reason = "Auto mod: User Sent several messages in a row." };

        if (AddPressure(spamTracker, message.Attachments.Count * 4.15))
            return new CheckSpamResult
            {
                IsSpam = true, Reason = "Auto mod: User Sent messages with several attachments"
            };

        if (AddPressure(spamTracker, message.Embeds.Count * 4.15))
            return new CheckSpamResult { IsSpam = true, Reason = "Auto mod: User Sent messages with several Embeds." };

        if (AddPressure(spamTracker, message.Content.Length * 0.00625))
            return new CheckSpamResult { IsSpam = true, Reason = "Auto mod: User sent too many characters." };

        if (AddPressure(spamTracker, message.Content.Split('\n').Length * 0.714))
            return new CheckSpamResult
            {
                IsSpam = true, Reason = "Auto mod: User sent too many new lines in a message."
            };

        if (AddPressure(spamTracker, (message.MentionedRoles.Count + message.MentionedUsers.Count) * 2.5))
            return new CheckSpamResult { IsSpam = true, Reason = "Auto mod: User sent too many pings in a message." };

        if (message.Content.Length > 0 && message.Content == spamTracker.MessageCache)
            if (AddPressure(spamTracker, 10))
                return new CheckSpamResult { IsSpam = true, Reason = "Auto mod: User sent too many duplicate Messages" };
        spamTracker.MessageCache = message.Content;
        spamTracker.DateTimeOffset = message.Timestamp;
        return new CheckSpamResult { IsSpam = false };
    }

    private static bool AddPressure(SpamTracker tracker, double pointsToAdd)
    {
        tracker.PointTotal += pointsToAdd;
        return tracker.PointTotal > 60;
    }

    private sealed record SpamTracker
    {
        public double PointTotal { get; set; }
        public string MessageCache { get; set; } = string.Empty;
        public ulong LastMessageId { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
    }

    public sealed record CheckSpamResult
    {
        public required bool IsSpam { get; init; }
        public string Reason { get; init; } = string.Empty;
    }
}
