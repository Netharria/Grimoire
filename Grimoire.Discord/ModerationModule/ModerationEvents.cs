// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Entities.AuditLogs;
using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.Moderation.Queries;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule;

[DiscordGuildBanAddedEventSubscriber]
[DiscordGuildBanRemovedEventSubscriber]
[DiscordMessageCreatedEventSubscriber]
[DiscordMessageReactionAddedEventSubscriber]
[DiscordGuildMemberAddedEventSubscriber]
public class ModerationEvents(IMediator mediator) :
    IDiscordGuildBanAddedEventSubscriber,
    IDiscordGuildBanRemovedEventSubscriber,
    IDiscordMessageCreatedEventSubscriber,
    IDiscordMessageReactionAddedEventSubscriber,
    IDiscordGuildMemberAddedEventSubscriber
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnGuildBanAdded(DiscordClient sender, GuildBanAddEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBanQuery
        {
            UserId = args.Member.Id,
            GuildId = args.Guild.Id
        });

        if (!response.ModerationModuleEnabled)
            return;
        if (response.LastSin is null || response.LastSin.SinOn < DateTimeOffset.UtcNow.AddSeconds(-5))
        {
            var addBanCommand = new AddBanCommand
            {
                GuildId = args.Guild.Id,
                UserId = args.Member.Id
            };
            try
            {
                var banAuditLog = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(DiscordAuditLogActionType.Ban, 1500);
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
                sender.Logger.Log(LogLevel.Information, ex, "Got exception when trying to access the audit log. Message: {},", ex.Message);
            }
            await this._mediator.Send(addBanCommand);

            response = await this._mediator.Send(new GetLastBanQuery
            {
                UserId = args.Member.Id,
                GuildId = args.Guild.Id
            });
        }
        if (response.LogChannelId is null) return;

        if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
            out var loggingChannel)) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor($"Banned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin?.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Red);
        if (response.LastSin?.ModeratorId is not null)
            embed.AddField("Mod", $"<@{response.LastSin.ModeratorId}>", true);

        embed.AddField("Reason", !string.IsNullOrWhiteSpace(response.LastSin?.Reason) ? response.LastSin.Reason : "None", true);

        await loggingChannel.SendMessageAsync(embed);
    }

    public async Task DiscordOnGuildBanRemoved(DiscordClient sender, GuildBanRemoveEventArgs args)
    {
        var response = await this._mediator.Send(new GetLastBanQuery
        {
            UserId = args.Member.Id,
            GuildId = args.Guild.Id
        });

        if (!response.ModerationModuleEnabled)
            return;

        if (response.LastSin is null)
            return;

        if (response.LogChannelId is null) return;

        if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
            out var loggingChannel)) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor($"Unbanned")
            .AddField("User", args.Member.Mention, true)
            .AddField("Sin Id", $"**{response.LastSin.SinId}**", true)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green);
        if (response.LastSin.ModeratorId is not null)
            embed.AddField("Mod", $"<@{response.LastSin.ModeratorId}>", true);

        await loggingChannel.SendMessageAsync(embed);
    }

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
    {
        if (!args.Channel.IsThread)
            return;
        if (args.Author is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(Permissions.ManageMessages))
            return;
        if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
            await args.Message.DeleteAsync();
    }

    public async Task DiscordOnMessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs args)
    {
        if (!args.Channel.IsThread)
            return;
        if (args.User is not DiscordMember member)
            return;
        if (args.Channel.PermissionsFor(member).HasPermission(Permissions.ManageMessages))
            return;
        if (await this._mediator.Send(new GetLockQuery { ChannelId = args.Channel.Id, GuildId = args.Guild.Id }))
            await args.Message.DeleteReactionAsync(args.Emoji, args.User, "Thread is locked.");
    }

    public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args)
    {
        var response = await this._mediator.Send(new GetUserMuteQuery
        {
            UserId = args.Member.Id,
            GuildId = args.Guild.Id
        });
        if (response is null) return;
        var role = args.Guild.Roles.GetValueOrDefault(response.Value);
        if (role is null) return;
        await args.Member.GrantRoleAsync(role, "Rejoined while muted");
    }
}
