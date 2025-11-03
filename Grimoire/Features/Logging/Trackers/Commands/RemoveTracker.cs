// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.Trackers.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.MessageLog)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
public sealed class RemoveTracker(SettingsModule settingsModule, GuildLog guildLog, TrackerLog trackerLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly TrackerLog _trackerLog = trackerLog;

    [Command("Untrack")]
    [Description("Stops the logging of the user's activity.")]
    public async Task UnTrackAsync(CommandContext ctx,
        [Parameter("User")] [Description("The user to stop logging.")]
        DiscordUser member)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var tracker = await this._settingsModule.RemoveTracker(member.GetUserId(), guild.GetGuildId());


        await ctx.EditReplyAsync(message: $"Tracker removed from {member.Mention}");

        if (tracker is not null)
            await this._trackerLog.SendTrackerMessageAsync(new TrackerMessage
            {
                GuildId = guild.GetGuildId(),
                TrackerId = tracker.LogChannelId.Value,
                TrackerIdType = TrackerIdType.ChannelId,
                Color = GrimoireColor.Purple,
                Description = $"{ctx.User.Username} removed a tracker on {member.Mention}"
            });

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = $"{ctx.User.Username} removed a tracker on {member.Mention}"
        });
    }
}
