// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Grimoire.Features.Shared.SpamModule;
public class SpamTrackerModule
{

    public sealed record SpamTracker
    {
        public double PointTotal { get; set; }
        public string MessageCache { get; set; } = string.Empty;
        public ulong LastMesssageId { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
    }

    public sealed record CheckSpamResult
    {
        public required bool IsSpam { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public ConcurrentDictionary<DiscordMember, SpamTracker> SpamUsers { get; set; } = new ConcurrentDictionary<DiscordMember, SpamTracker>();

    public CheckSpamResult CheckSpam(DiscordMessage message)
    {
        if (message.Author is not DiscordMember member)
            return new CheckSpamResult { IsSpam = false };

        if (member.Permissions.HasPermission(DiscordPermissions.ManageChannels) || member.IsOwner)
            return new CheckSpamResult { IsSpam = false };
        if (!this.SpamUsers.TryGetValue(member, out var spamTracker))
        {
            spamTracker = new SpamTracker();
            if (!this.SpamUsers.TryAdd(member, spamTracker))
                throw new ArgumentException("Tried to add spam tracker but it already existed");
        }
        if (message.Id == spamTracker.LastMesssageId)
            return new CheckSpamResult { IsSpam = false };

        spamTracker.LastMesssageId = message.Id;

        spamTracker.PointTotal -= (message.Timestamp - spamTracker.DateTimeOffset).TotalSeconds * 2;

        if (spamTracker.PointTotal < 0)
            spamTracker.PointTotal = 0;

        if (AddPresure(spamTracker, message, 10))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User Sent several messsages in a row." };

        if (AddPresure(spamTracker, message, message.Attachments.Count * 4.15))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User Sent messages with several attachments" };

        if (AddPresure(spamTracker, message, message.Embeds.Count * 4.15))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User Sent messages with several Embeds." };

        if (AddPresure(spamTracker, message, message.Content.Length * 0.00625))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User sent too many characters." };

        if (AddPresure(spamTracker, message, message.Content.Split('\n').Length * 0.714))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User sent too many new lines in a message." };

        if (AddPresure(spamTracker, message, (message.MentionedRoles.Count + message.MentionedUsers.Count) * 2.5))
            return new CheckSpamResult { IsSpam = true, Reason = "Automod: User sent too many pings in a message." };

        if (message.Content.Length > 0 && message.Content == spamTracker.MessageCache)
            if (AddPresure(spamTracker, message, 10))
                return new CheckSpamResult { IsSpam = true, Reason = "Automod: User sent too many duplicate Messages" };
        spamTracker.MessageCache = message.Content;
        spamTracker.DateTimeOffset = message.Timestamp;
        return new CheckSpamResult { IsSpam = false };
    }

    public static bool AddPresure(SpamTracker tracker, DiscordMessage message, double pointsToAdd)
    {
        tracker.PointTotal += pointsToAdd;
        return tracker.PointTotal > 60;
    }


}
