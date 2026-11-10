// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Logging.UserLogging;

public sealed class UpdatedAvatarEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    IDiscordImageEmbedService imageEmbedService,
    SettingsModule settingsModule,
    GuildLog guildLog)
    : IEventHandler<GuildMemberUpdatedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
    {
        if (!await settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()).GetOrElse(() => false))
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var currentAvatar = await dbContext.Avatars
            .AsNoTracking()
            .Where(x => x.UserId == args.Member.GetUserId() && x.GuildId == args.Guild.GetGuildId())
            .OrderByDescending(x => x.Timestamp)
            .Select(x => (AvatarFileName?)x.FileName)
            .FirstOrDefaultAsync();
        var newAvatar = args.MemberAfter.GetAvatarFileName();
        // No row yet means MemberAdded hasn't stored the first one, and there is nothing to compare against.
        if (currentAvatar is not { } oldAvatar || oldAvatar.Equals(newAvatar, StringComparison.Ordinal))
            return;

        await dbContext.Avatars.AddAsync(
            new Avatar
            {
                GuildId = args.Guild.GetGuildId(),
                UserId = args.Member.GetUserId(),
                FileName = newAvatar,
                Timestamp = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        // The history is kept either way, but the log downloads the new avatar to re-upload it.
        if (!await settingsModule.IsLogChannelSet(GuildLogType.AvatarUpdated, args.Guild.GetGuildId()))
            return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Avatar Updated")
            .WithDescription($"**User:** {args.Member.Mention}\n\n" +
                             $"Old avatar in thumbnail. New avatar down below")
            .WithThumbnail(oldAvatar.Value)
            .WithColor(GrimoireColor.Purple)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomMessage
        {
            GuildId = args.Guild.GetGuildId(),
            GuildLogType = GuildLogType.AvatarUpdated,
            Message = await imageEmbedService
                .BuildImageEmbedAsync(
                    [newAvatar.Value],
                    args.Member.GetUserId(),
                    embed,
                    false)
        });
    }
}
