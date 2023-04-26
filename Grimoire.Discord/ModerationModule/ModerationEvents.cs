// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.BanCommands.AddBan;
using Grimoire.Core.Features.Moderation.Queries.GetLastBan;
using Grimoire.Core.Features.Moderation.Queries.GetLock;
using Grimoire.Core.Features.Moderation.Queries.GetUserMute;
using Microsoft.Extensions.Logging;

namespace Grimoire.Discord.ModerationModule
{
    [DiscordGuildBanAddedEventSubscriber]
    [DiscordGuildBanRemovedEventSubscriber]
    [DiscordMessageCreatedEventSubscriber]
    [DiscordMessageReactionAddedEventSubscriber]
    [DiscordGuildMemberAddedEventSubscriber]
    public class ModerationEvents :
        IDiscordGuildBanAddedEventSubscriber,
        IDiscordGuildBanRemovedEventSubscriber,
        IDiscordMessageCreatedEventSubscriber,
        IDiscordMessageReactionAddedEventSubscriber,
        IDiscordGuildMemberAddedEventSubscriber
    {
        private readonly IMediator _mediator;

        public ModerationEvents(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public async Task DiscordOnGuildBanAdded(DiscordClient sender, GuildBanAddEventArgs args)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var response = await this._mediator.Send(new GetLastBanQuery());

            if (!response.ModerationModuleEnabled)
                return;

            if (response.SinOn > DateTimeOffset.UtcNow.AddSeconds(-5))
            {
                var addBanCommand = new AddBanCommand
                {
                    GuildId = args.Guild.Id,
                    UserId = args.Member.Id
                };
                try
                {
                    var banAuditLog = await args.Guild.GetRecentAuditLogAsync<DiscordAuditLogBanEntry>(AuditLogActionType.Ban, 1500);
                    if (banAuditLog is not null && banAuditLog.Target.Id == args.Member.Id)
                    {
                        addBanCommand.ModeratorId = banAuditLog.UserResponsible.Id;
                        addBanCommand.Reason = banAuditLog.Reason;
                    }
                }
                catch (Exception ex) when (
                ex is UnauthorizedException
                || ex is ServerErrorException)
                {
                    sender.Logger.Log(LogLevel.Information, ex, "Got exception when trying to access the audit log. Message: {},", ex.Message);
                }
                await this._mediator.Send(addBanCommand);

                response = await this._mediator.Send(new GetLastBanQuery());
            }
            if (response.LogChannelId is null) return;

            if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

            var banMessage = $"**Banned:** {args.Member.GetUsernameWithDiscriminator()} (ID {args.Member.Id})\n" +
                            $"**SinId:** {response.SinId}\n" +
                            $"**Mod:** <@{response.ModeratorId}>\n" +
                            $"**Reason:** {(!string.IsNullOrWhiteSpace(response.Reason) ? response.Reason : "(no reason specified)")}\n";

            if (string.IsNullOrWhiteSpace(response.Reason))
            {
                banMessage += $"Reason can be updated with `/reason {response.SinId} <your reason here>`";
            }
            else
            {
                banMessage += $"This can be published to the published to the public log channel with `/publish ban {response.SinId}`";
            }

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithTitle($"{args.Member.GetUsernameWithDiscriminator()} banned")
                .WithDescription(banMessage)
                .WithColor(GrimoireColor.Orange));
        }

        public async Task DiscordOnGuildBanRemoved(DiscordClient sender, GuildBanRemoveEventArgs args)
        {
            var response = await this._mediator.Send(new GetLastBanQuery());

            if (!response.ModerationModuleEnabled)
                return;

            if (response.LogChannelId is null) return;

            if (!args.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithTitle($"{args.Member.GetUsernameWithDiscriminator()} unbanned")
                .WithDescription($"**Sin Id:** {response.SinId}\n" +
                $"**Unbanned:** {args.Member.GetUsernameWithDiscriminator()} (ID {args.Member.Id})\n" +
                $"`/pardon {response.SinId} <reason>` to add an unban reason." +
                $"Then `/publish unban {response.SinId}` to publish to public log channel.")
                .WithColor(GrimoireColor.Green));
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
                await args.Message.DeleteAsync();
        }

        public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args)
        {
            var response = await _mediator.Send(new GetUserMuteQuery
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
}
