// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Features.Moderation.Ban;
using Grimoire.Features.Moderation.Commands;
using Grimoire.Features.Moderation.Lock;
using Grimoire.Features.Moderation.Mute;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Moderation;

public sealed partial class ModerationEvents(IMediator mediator, ILogger<ModerationEvents> logger)
{
    private readonly ILogger<ModerationEvents> _logger = logger;
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnGuildBanAdded(DiscordClient sender, GuildBanAddedEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBanQuery
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });

        if (!response.ModerationModuleEnabled)
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
                    addBanCommand.ModeratorId = banAuditLog?.UserResponsible?.Id;
                    addBanCommand.Reason = banAuditLog?.Reason ?? "";
                }
            }
            catch (Exception ex) when (
                ex is UnauthorizedException
                || ex is ServerErrorException)
            {
                LogAuditException(this._logger, ex);
            }

            await this._mediator.Send(addBanCommand);

            response = await this._mediator.Send(new GetLastBanQuery
            {
                UserId = args.Member.Id, GuildId = args.Guild.Id
            });
        }

        if (response.LogChannelId is null) return;

        if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Banned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin?.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Red);
        if (response.LastSin?.ModeratorId is not null)
            embed.AddField("Mod", UserExtensions.Mention(response.LastSin.ModeratorId), true);

        embed.AddField("Reason",
            !string.IsNullOrWhiteSpace(response.LastSin?.Reason) ? response.LastSin.Reason : "None", true);

        await loggingChannel.SendMessageAsync(embed);
    }

    [LoggerMessage(LogLevel.Information, "Exception while accessing audit log.")]
    static partial void LogAuditException(ILogger<ModerationEvents> logger, Exception ex);

    public async Task DiscordOnGuildBanRemoved(DiscordClient sender, GuildBanRemovedEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBanQuery
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });

        if (!response.ModerationModuleEnabled)
            return;

        if (response.LastSin is null)
            return;

        if (response.LogChannelId is null) return;

        if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unbanned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green);
        if (response.LastSin.ModeratorId is not null)
            embed.AddField("Mod", UserExtensions.Mention(response.LastSin.ModeratorId), true);

        await loggingChannel.SendMessageAsync(embed);
    }

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreatedEventArgs args)
    {
        if (!args.Channel.IsThread)
            return;
        if (args.Author is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermissions.ManageMessages))
            return;
        if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
            await args.Message.DeleteAsync();
    }

    public async Task DiscordOnMessageReactionAdded(DiscordClient sender, MessageReactionAddedEventArgs args)
    {
        if (!args.Channel.IsThread)
            return;
        if (args.User is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(DiscordPermissions.ManageMessages))
            return;
        if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
            await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
    }

    public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        var response = await this._mediator.Send(new GetUserMuteQuery
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });
        if (response is null) return;
        var role = args.Guild.Roles.GetValueOrDefault(response.Value);
        if (role is null) return;
        await args.Member.GrantRoleAsync(role, "Rejoined while muted");
    }
}
