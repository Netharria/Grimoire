// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Ban.Shared;
using Grimoire.Features.Shared.Channels.GuildLog;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation.Ban.Events;

public partial class BanAddedEvent(IMediator mediator, ILogger<BanAddedEvent> logger, GuildLog guildLog)
    : IEventHandler<GuildBanAddedEventArgs>
{
    private readonly ILogger<BanAddedEvent> _logger = logger;
    private readonly GuildLog _guildLog = guildLog;
    private readonly IMediator _mediator = mediator;

    public async Task HandleEventAsync(DiscordClient sender, GuildBanAddedEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBan.Query
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });

        if (response is null || !response.ModerationModuleEnabled)
            return;
        if (response.LastSin is null || response.LastSin.SinOn < DateTimeOffset.UtcNow.AddSeconds(-5))
        {
            var addBanCommand = new AddBan.Command { GuildId = args.Guild.Id, UserId = args.Member.Id };
            try
            {
                var banAuditLog =
                    await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(DiscordAuditLogActionType.Ban,
                        1500);
                if (banAuditLog is not null && banAuditLog.Target.Id == args.Member.Id)
                {
                    addBanCommand.ModeratorId = banAuditLog.UserResponsible?.Id;
                    addBanCommand.Reason = banAuditLog.Reason ?? "";
                }
            }
            catch (Exception ex) when (
                ex is UnauthorizedException or ServerErrorException)
            {
                LogAuditException(this._logger, ex);
            }

            await this._mediator.Send(addBanCommand);

            response = await this._mediator.Send(new GetLastBan.Query
            {
                UserId = args.Member.Id, GuildId = args.Guild.Id
            });
        }

        if (response is null || !response.ModerationModuleEnabled)
            return;

        var builder = new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin?.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Red);
        if (response.LastSin?.ModeratorId is not null)
            builder.AddField("Mod", UserExtensions.Mention(response.LastSin.ModeratorId), true);

        builder.AddField("Reason",
            !string.IsNullOrWhiteSpace(response.LastSin?.Reason) ? response.LastSin.Reason : "None", true);

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Embed = builder
        });
    }

    [LoggerMessage(LogLevel.Information, "Exception while accessing audit log.")]
    static partial void LogAuditException(ILogger<BanAddedEvent> logger, Exception ex);
}
