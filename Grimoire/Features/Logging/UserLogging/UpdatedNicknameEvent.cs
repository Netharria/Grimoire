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

public sealed class UpdatedNicknameEvent(
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
        var currentNickname = await dbContext.NicknameHistory
            .AsNoTracking()
            .Where(x => x.GuildId == args.Guild.GetGuildId() && x.UserId == args.Member.GetUserId())
            .OrderByDescending(x => x.Timestamp)
            .Select(y => y.Nickname)
            .FirstOrDefaultAsync();

        var after = Nickname.CreateIfNotEmpty(args.NicknameAfter);

        if (currentNickname is null
            || currentNickname == after
            || (currentNickname.HasValue && after.HasValue
                                         && currentNickname.Value.Equals(after.Value,
                                             StringComparison.CurrentCultureIgnoreCase)))
            return;

        await dbContext.NicknameHistory.AddAsync(
            new NicknameHistory
            {
                GuildId = args.Guild.GetGuildId(), UserId = args.Member.GetUserId(), Nickname = after
            });
        await dbContext.SaveChangesAsync();

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(),
            GuildLogType = GuildLogType.NicknameUpdated,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Nickname Updated")
                .AddField("User", args.Member.Mention)
                .AddField("Before", currentNickname?.Value ?? "`None`", true)
                .AddField("After",
                    string.IsNullOrWhiteSpace(args.NicknameAfter)
                        ? "`None`"
                        : args.NicknameAfter, true)
                .WithThumbnail(args.Member.GetGuildAvatarUrl(MediaFormat.Auto))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Mint)
        });
    }
}
