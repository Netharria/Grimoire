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

namespace Grimoire.Features.Logging.UserLogging;

public sealed class UpdatedUsernameEvent(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule, GuildLog guildLog, TrackerLog trackerLog) : IEventHandler<GuildMemberUpdatedEventArgs>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
        private readonly SettingsModule _settingsModule = settingsModule;
        private readonly GuildLog _guildLog = guildLog;
        private readonly TrackerLog _trackerLog = trackerLog;

        public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
        {
            if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.Id))
                return;

            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
            var currentUsername = await dbContext.UsernameHistory
                .AsNoTracking()
                .Where(member => member.UserId == args.MemberAfter.Id)
                .Where(usernameHistory => usernameHistory.Timestamp < DateTime.UtcNow.AddSeconds(-2))
                .OrderByDescending(x => x.Timestamp)
                .Select(member => member.Username)
                .FirstOrDefaultAsync();
            if (currentUsername is null
                || string.Equals(currentUsername, args.UsernameAfter, StringComparison.CurrentCultureIgnoreCase))
                return ;

            await dbContext.UsernameHistory.AddAsync(
                new UsernameHistory { UserId = args.Member.Id, Username = args.UsernameAfter });
            await dbContext.SaveChangesAsync();

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.UsernameUpdated,
                Embed = new DiscordEmbedBuilder()
                    .WithAuthor("Username Updated")
                    .AddField("User", args.MemberAfter.Mention)
                    .AddField("Before",
                        string.IsNullOrWhiteSpace(currentUsername)
                            ? "`Unknown`"
                            : currentUsername, true)
                    .AddField("After",
                        string.IsNullOrWhiteSpace(args.UsernameAfter)
                            ? "`Unknown`"
                            : args.UsernameAfter, true)
                    .WithThumbnail(args.MemberAfter.GetAvatarUrl(MediaFormat.Auto))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Mint)
            });

            await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomEmbed
            {
                GuildId = args.Guild.Id,
                TrackerId = args.Member.Id,
                TrackerIdType = TrackerIdType.UserId,
                Embed = new DiscordEmbedBuilder()
                    .WithAuthor("Username Updated")
                    .AddField("User", UserExtensions.Mention(args.Member.Id))
                    .AddField("Before",
                        string.IsNullOrWhiteSpace(currentUsername) ? "`Unknown`" : currentUsername,
                        true)
                    .AddField("After",
                        string.IsNullOrWhiteSpace(args.UsernameAfter) ? "`Unknown`" : args.UsernameAfter,
                        true)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Mint)
            });
        }
    }
