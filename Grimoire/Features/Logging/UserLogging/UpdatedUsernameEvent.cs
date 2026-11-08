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

public sealed class UpdatedUsernameEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog) : IEventHandler<GuildMemberUpdatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberUpdatedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.UserLog, args.Guild.GetGuildId()).GetOrElse(() => false))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var currentUsername = await dbContext.UsernameHistory
            .AsNoTracking()
            .Where(member => member.UserId == args.MemberAfter.GetUserId())
            .Where(usernameHistory => usernameHistory.Timestamp < DateTime.UtcNow.AddSeconds(-2))
            .OrderByDescending(x => x.Timestamp)
            .Select(member => (Username?)member.Username)
            .FirstOrDefaultAsync();

        var after = Username.Create(args.UsernameAfter);

        // No row yet means MemberAdded hasn't stored the first one, and there is nothing to compare against.
        if (currentUsername is not { } before || before.Equals(after, StringComparison.CurrentCultureIgnoreCase))
            return;

        await dbContext.UsernameHistory.AddAsync(
            new UsernameHistory
            {
                UserId = args.Member.GetUserId(), Username = after, Timestamp = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(),
            GuildLogType = GuildLogType.UsernameUpdated,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Username Updated")
                .AddField("User", args.MemberAfter.Mention)
                .AddField("Before", before.Value, true)
                .AddField("After", after.Value, true)
                .WithThumbnail(args.MemberAfter.GetAvatarUrl(MediaFormat.Auto))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint)
        });
    }
}
