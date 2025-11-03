// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.Channels.TrackerLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Logging.Trackers;

public sealed class RemoveExpiredTrackers
{
    internal sealed class BackgroundTask(
        IServiceProvider serviceProvider,
        SettingsModule settingsModule,
        ILogger<BackgroundTask> logger)
        : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromSeconds(5))
    {
        private readonly SettingsModule _settingsModule = settingsModule;

        protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var discord = serviceProvider.GetRequiredService<DiscordClient>();
            var guildLog = serviceProvider.GetRequiredService<GuildLog>();
            var trackerLog = serviceProvider.GetRequiredService<TrackerLog>();
            var expiredTrackers = await this._settingsModule.RemoveAllExpiredTrackers(cancellationToken);
            foreach (var expiredTracker in expiredTrackers)
            {
                var user = await discord.GetUserOrDefaultAsync(expiredTracker.UserId);

                await trackerLog.SendTrackerMessageAsync(
                    new TrackerMessage
                    {
                        GuildId = expiredTracker.GuildId,
                        TrackerId = expiredTracker.LogChannelId.Value,
                        TrackerIdType = TrackerIdType.ChannelId,
                        Description = $"Tracker on {user?.Mention} has expired."
                    }, cancellationToken);

                await guildLog.SendLogMessageAsync(
                    new GuildLogMessage
                    {
                        GuildId = expiredTracker.GuildId,
                        GuildLogType = GuildLogType.Moderation,
                        Description = $"Tracker on {user?.Mention} has expired."
                    }, cancellationToken);
            }
        }
    }
}
