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
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Ban.Events;

public partial class BanAddedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    ILogger<BanAddedEvent> logger)
    : IEventHandler<GuildBanAddedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<BanAddedEvent> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanAddedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Moderation, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var lastBan = await dbContext.Sins
            .AsNoTracking()
            .Where(m => m.UserId == args.Member.GetUserId() && m.GuildId == args.Guild.GetGuildId())
            .Where(sin => sin.SinType == SinType.Ban)
            .OrderByDescending(x => x.SinOn)
            .Select(sin => new LastSin
            {
                SinId = sin.Id,
                Actor = sin.Actor,
                // ReSharper disable once AccessToDisposedClosure
                Reason = dbContext.SinReasonHistory
                    .Where(r => r.SinId == sin.Id)
                    .OrderByDescending(r => r.SetAt)
                    .Select(r => r.Reason.Value)
                    .FirstOrDefault() ?? string.Empty,
                SinOn = sin.SinOn
            })
            .FirstOrDefaultAsync();
        if (lastBan is null || lastBan.SinOn < DateTimeOffset.UtcNow.AddSeconds(-30))
            try
            {
                var banAuditLog =
                    await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(DiscordAuditLogActionType.Ban,
                        1500);

                var auditReason = banAuditLog?.Target.Id != args.Member.Id
                    ? string.Empty
                    : banAuditLog.Reason ?? string.Empty;
                ModerationActor auditActor = banAuditLog?.Target.Id != args.Member.Id
                    ? new ModerationActor.System()
                    : banAuditLog.UserResponsible?.Id is not null
                        ? new ModerationActor.Moderator(new ModeratorId(banAuditLog.UserResponsible.Id))
                        : new ModerationActor.System();

                var now = DateTimeOffset.UtcNow;
                var sin = Sin.ForBan(auditActor, args.Member.GetUserId(), args.Guild.GetGuildId(), now)
                    .Match(
                        s => string.IsNullOrWhiteSpace(auditReason)
                            ? s
                            : s with
                            {
                                ReasonHistory =
                                [
                                    new SinReasonHistory
                                    {
                                        SinId = default,
                                        Reason = ModerationReason.Create(auditReason)
                                            .Match(r => r, _ => throw new UnreachableException()),
                                        Actor = auditActor,
                                        SetAt = now
                                    }
                                ]
                            },
                        _ => throw new UnreachableException());
                dbContext.Sins.Add(sin);
                await dbContext.SaveChangesAsync();

                lastBan = new LastSin { SinId = sin.Id, Actor = sin.Actor, Reason = auditReason, SinOn = sin.SinOn };
            }
            catch (Exception ex) when (ex is UnauthorizedException or ServerErrorException)
            {
                LogAuditException(this._logger, ex);
            }

        if (lastBan is null)
            return;

        var builder = new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{lastBan.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Red);
        if (lastBan.Actor is ModerationActor.Moderator)
            builder.AddField("Mod", UserExtensions.Mention(lastBan.Actor), true);

        builder.AddField("Reason",
            !string.IsNullOrWhiteSpace(lastBan.Reason) ? lastBan.Reason : "None", true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = builder
        });
    }

    [LoggerMessage(LogLevel.Information, "Exception while accessing audit log.")]
    static partial void LogAuditException(ILogger<BanAddedEvent> logger, Exception ex);

    private sealed record LastSin
    {
        public SinId SinId { get; init; }
        public ModerationActor Actor { get; init; } = new ModerationActor.System();
        public string Reason { get; init; } = string.Empty;
        public DateTimeOffset SinOn { get; init; }
    }
}
