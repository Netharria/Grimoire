// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.GuildIssues;
using Grimoire.Features.Logging.UserLogging;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Events;

public sealed partial class InviteEvents(
    IInviteService inviteService,
    GuildIssueReporter issueReporter,
    ILogger<InviteEvents> logger)
    : IEventHandler<InviteCreatedEventArgs>,
        IEventHandler<InviteDeletedEventArgs>
{
    private readonly IInviteService _inviteService = inviteService;
    private readonly ILogger _logger = logger;

    public Task HandleEventAsync(DiscordClient sender, InviteCreatedEventArgs args)
    {
        this._inviteService.UpdateInvite(args.Guild.GetGuildId(), args.Invite.ToInvite());
        return Task.CompletedTask;
    }

    /// <remarks>
    ///     A deleted invite is forgotten if it expired or someone deleted it. One that was used up stays, so the join
    ///     it caused can still be matched to it. Forgetting an invite that was never tracked is fine either way.
    /// </remarks>
    public async Task HandleEventAsync(DiscordClient sender, InviteDeletedEventArgs args)
    {
        if (args.Invite.ExpiresAt < DateTime.UtcNow)
        {
            this._inviteService.DeleteInvite(args.Guild.GetGuildId(), args.Invite.GetInviteCode());
            return;
        }

        DiscordAuditLogInviteEntry? deletedInviteEntry;
        try
        {
            deletedInviteEntry = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogInviteEntry>(
                DiscordAuditLogActionType.InviteDelete, 1500);
        }
        catch (UnauthorizedException)
        {
            await issueReporter.ReportAsync(GuildIssueCatalog.CannotReadAuditLog(args.Guild.GetGuildId()));
            return;
        }

        if (deletedInviteEntry is null)
        {
            if (args.Invite.MaxUses != args.Invite.Uses + 1)
                LogAuditError(this._logger);
            return;
        }

        if (deletedInviteEntry.Target.Code == args.Invite.Code)
            this._inviteService.DeleteInvite(args.Guild.GetGuildId(), args.Invite.GetInviteCode());
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to retrieve audit log entry for deleted invite.")]
    static partial void LogAuditError(ILogger logger);
}
