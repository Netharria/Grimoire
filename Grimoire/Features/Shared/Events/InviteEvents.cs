// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using Grimoire.Features.Logging.UserLogging;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Shared.Events;

public sealed partial class InviteEvents(
    IInviteService inviteService,
    ILogger<InviteEvents> logger)
    : IEventHandler<InviteCreatedEventArgs>,
        IEventHandler<InviteDeletedEventArgs>
{
    private readonly IInviteService _inviteService = inviteService;
    private readonly ILogger _logger = logger;

    public Task HandleEventAsync(DiscordClient sender, InviteCreatedEventArgs args)
    {
        this._inviteService.UpdateInvite(args.Guild.Id,
            new Invite
            {
                Code = args.Invite.Code,
                Inviter = args.Invite.Inviter.GetUsernameWithDiscriminator(),
                Url = args.Invite.ToString(),
                Uses = args.Invite.Uses,
                MaxUses = args.Invite.MaxUses
            });
        return Task.CompletedTask;
    }

    public async Task HandleEventAsync(DiscordClient sender, InviteDeletedEventArgs args)
    {
        if (args.Invite.ExpiresAt < DateTime.UtcNow)
        {
            if (this._inviteService.DeleteInvite(args.Guild.Id, args.Invite.Code))
                return;
            throw new Exception("Was not able to delete expired invite");
        }

        var deletedInviteEntry =
            await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogInviteEntry>(DiscordAuditLogActionType.InviteDelete,
                1500);
        if (deletedInviteEntry == null)
        {
            if (args.Invite.MaxUses != args.Invite.Uses + 1)
                LogAuditError(this._logger);
            return;
        }

        if (deletedInviteEntry.Target.Code == args.Invite.Code)
            if (!this._inviteService.DeleteInvite(args.Guild.Id, args.Invite.Code))
                throw new Exception("Was not able to delete expired invite");
    }

    [LoggerMessage(LogLevel.Warning, "Was not able to retrieve audit log entry for deleted invite.")]
    public static partial void LogAuditError(ILogger logger);
}
