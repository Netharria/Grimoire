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

public sealed class UpdatedAvatarEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IDiscordImageEmbedService imageEmbedService,
    SettingsModule settingsModule,
    GuildLog guildLog,
    TrackerLog trackerLog)
    : IEventHandler<GuildMemberUpdatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly IDiscordImageEmbedService _imageEmbedService = imageEmbedService;
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly TrackerLog _trackerLog = trackerLog;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var currentAvatar = await dbContext.Avatars
            .AsNoTracking()
            .Where(x => x.UserId == args.Member.GetUserId() && x.GuildId == args.Guild.GetGuildId())
            .OrderByDescending(x => x.Timestamp)
            .Select(x => x.FileName)
            .FirstOrDefaultAsync();
        if ( string.Equals(currentAvatar.Value, args.MemberAfter.AvatarUrl, StringComparison.Ordinal))
            return;

        await dbContext.Avatars.AddAsync(
            new Avatar { GuildId = args.Guild.GetGuildId(), UserId = args.Member.GetUserId(), FileName = args.MemberAfter.GetAvatarFileName() });
        await dbContext.SaveChangesAsync();

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Avatar Updated")
            .WithDescription($"**User:** {args.Member.Mention}\n\n" +
                             $"Old avatar in thumbnail. New avatar down below")
            .WithThumbnail(currentAvatar)
            .WithColor(GrimoireColor.Purple)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
        {
            GuildId = args.Guild.GetGuildId(),
            GuildLogType = GuildLogType.AvatarUpdated,
            Message = await this._imageEmbedService
                .BuildImageEmbedAsync(
                    [args.MemberAfter.AvatarUrl],
                    args.Member.GetUserId(),
                    embed,
                    false)
        });

        await this._trackerLog.SendTrackerMessageAsync(new TrackerMessageCustomMessage
        {
            GuildId = args.Guild.GetGuildId(),
            TrackerId = args.Member.Id,
            TrackerIdType = TrackerIdType.UserId,
            Message = await this._imageEmbedService.BuildImageEmbedAsync(
                [args.MemberAfter.AvatarUrl],
                args.Member.GetUserId(),
                new DiscordEmbedBuilder()
                    .WithAuthor("Avatar Updated")
                    .AddField("User", UserExtensions.Mention(args.Member.GetUserId()))
                    .WithThumbnail(args.MemberAfter.AvatarUrl)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .WithColor(GrimoireColor.Purple),
                false)
        });
    }
}
