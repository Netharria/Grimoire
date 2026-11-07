// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Ban.Events;

public class BanRemovedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    BanHandoff banHandoff) : IEventHandler<GuildBanRemovedEventArgs>
{
    private readonly BanHandoff _banHandoff = banHandoff;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanRemovedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Moderation, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        var unbannedByBot = this._banHandoff.TakeUnban(args.Guild.GetGuildId(), args.Member.GetUserId());

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var lastBan = await dbContext.Sins
            .AsNoTracking()
            .Where(m => m.UserId == args.Member.GetUserId() && m.GuildId == args.Guild.GetGuildId())
            .Where(sin => sin.SinType == SinType.Ban)
            .OrderByDescending(x => x.SinOn)
            .Select(sin => new { SinId = sin.Id, sin.Actor })
            .FirstOrDefaultAsync();

        if (lastBan is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unbanned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{lastBan.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green);
        if (lastBan.Actor is ModerationActor.Moderator)
            embed.AddField("Banned by", UserExtensions.Mention(lastBan.Actor), true);
        if ((unbannedByBot ?? await FindWhoUnbannedAsync(args)) is { } unbannedBy)
            embed.AddField("Unbanned by", UserExtensions.Mention(unbannedBy), true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }

    /// <summary>An unban made outside the bot. Who did it is a nicety, so a log the bot can't read just leaves it out.</summary>
    private static async Task<ModeratorId?> FindWhoUnbannedAsync(GuildBanRemovedEventArgs args)
    {
        try
        {
            var entry = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(DiscordAuditLogActionType.Unban, 1500);
            return entry?.Target.Id == args.Member.Id && entry.UserResponsible?.Id is { } moderatorId
                ? new ModeratorId(moderatorId)
                : null;
        }
        catch (DiscordException)
        {
            return null;
        }
    }
}
