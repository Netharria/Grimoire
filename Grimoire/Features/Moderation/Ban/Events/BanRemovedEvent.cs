// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.Ban.Events;

public class BanRemovedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog) : IEventHandler<GuildBanRemovedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanRemovedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Moderation, args.Guild.Id))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var lastBan = await dbContext.Sins
            .AsNoTracking()
            .Where(m => m.UserId == args.Member.Id && m.GuildId == args.Guild.Id)
            .Where(sin => sin.SinType == SinType.Ban)
            .OrderByDescending(x => x.SinOn)
            .Select(sin => new LastSin { SinId = sin.Id, ModeratorId = sin.ModeratorId, SinOn = sin.SinOn })
            .FirstOrDefaultAsync();

        if (lastBan is null)
            return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unbanned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{lastBan.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green);
        if (lastBan.ModeratorId is not null)
            embed.AddField("Mod", UserExtensions.Mention(lastBan.ModeratorId), true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });
    }

    private sealed record LastSin
    {
        public long SinId { get; init; }
        public ulong? ModeratorId { get; init; }
        public DateTimeOffset SinOn { get; init; }
    }
}
