// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Ban.Events;

public partial class BanAddedEvent(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    GuildLog guildLog,
    GuildIssueReporter issueReporter,
    BanHandoff banHandoff,
    ILogger<BanAddedEvent> logger)
    : IEventHandler<GuildBanAddedEventArgs>
{
    private readonly BanHandoff _banHandoff = banHandoff;
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly ILogger<BanAddedEvent> _logger = logger;
    private readonly SettingsModule _settingsModule = settingsModule;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanAddedEventArgs args)
    {
        if (!await this._settingsModule.IsModuleEnabled(Module.Moderation, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        var pending = this._banHandoff.TakeBan(args.Guild.GetGuildId(), args.Member.GetUserId());
        var (actor, reason) = pending is not null
            ? (new ModerationActor.Moderator(pending.Moderator), pending.Reason)
            : await this.FindWhoBannedAsync(args);

        var sinId = await this.RecordSinAsync(args, actor, reason);
        pending?.MarkRecorded(sinId);

        var builder = new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{sinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Red);
        if (actor is ModerationActor.Moderator)
            builder.AddField("Mod", UserExtensions.Mention(actor), true);

        builder.AddField("Reason", !string.IsNullOrWhiteSpace(reason) ? reason : "None", true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = builder
        });
    }

    private async Task<SinId> RecordSinAsync(GuildBanAddedEventArgs args, ModerationActor actor, string reason)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;
        // An unusable reason, such as one over 1000 characters from the audit log, is left out, not a lost ban.
        var sin = Sin.ForBan(actor, args.Member.GetUserId(), args.Guild.GetGuildId(), now,
                ModerationReason.CreateIfNotBlank(reason).Match(r => r, _ => null))
            .Match(s => s, _ => throw new UnreachableException("Discord ids are never zero."));
        dbContext.Sins.Add(sin);
        await dbContext.SaveChangesAsync();
        return sin.Id;
    }

    /// <summary>A ban made outside the bot. If the audit log can't say who did it, the ban is still recorded.</summary>
    private async Task<(ModerationActor Actor, string Reason)> FindWhoBannedAsync(GuildBanAddedEventArgs args)
    {
        try
        {
            var entry = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(DiscordAuditLogActionType.Ban, 1500);
            // Without a matching entry, the ban itself still has the reason, below.
            if (entry?.Target.Id == args.Member.Id)
                return (entry.UserResponsible?.Id is { } moderatorId
                    ? new ModerationActor.Moderator(new ModeratorId(moderatorId))
                    : new ModerationActor.System(), entry.Reason ?? string.Empty);
        }
        catch (UnauthorizedException ex)
        {
            LogAuditException(this._logger, ex);
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotReadAuditLog(args.Guild.GetGuildId()));
        }
        catch (ServerErrorException ex)
        {
            LogAuditException(this._logger, ex);
        }

        return (new ModerationActor.System(), await GetBanReasonAsync(args));
    }

    private static async Task<string> GetBanReasonAsync(GuildBanAddedEventArgs args)
    {
        try
        {
            return (await args.Guild.GetBanAsync(args.Member)).Reason ?? string.Empty;
        }
        catch (DiscordException)
        {
            return string.Empty;
        }
    }

    [LoggerMessage(LogLevel.Information, "Exception while accessing audit log.")]
    static partial void LogAuditException(ILogger<BanAddedEvent> logger, Exception ex);
}
