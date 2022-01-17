// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Leveling.Commands.GainUserXp;
using Cybermancy.Enums;
using Cybermancy.Utilities;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MediatR;
using Nefarius.DSharpPlus.Extensions.Hosting.Attributes;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.LevelingModule
{
    [DiscordMessageEventsSubscriber]
    public class LevelingEvents : IDiscordMessageEventsSubscriber
    {
        private readonly IMediator _mediator;

        public LevelingEvents(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Message.MessageType is not MessageType.Default or MessageType.Reply ||
                args.Author is not DiscordMember member) return;
            if (member.IsBot) return;
            var response = await this._mediator.Send(new GainUserXpCommand
            {
                ChannelId = args.Channel.Id,
                GuildId = args.Guild.Id,
                UserId = member.Id,
                RoleIds = member.Roles.Select(x => x.Id).ToArray()
            });
            if (!response.Success) return;

            var rolesToAdd = response.EarnedRewards
                .Join(args.Guild.Roles, x => x, y => y.Key, (x, y) => y.Value)
                .Concat(member.Roles)
                .Distinct()
                .ToArray();

            if (rolesToAdd.Except(member.Roles).Any())
                await member.ReplaceRolesAsync(rolesToAdd);

            if (response.LoggingChannel is null) return;

            if (!args.Guild.Channels.TryGetValue(response.LoggingChannel.Value,
                out var loggingChannel)) return;

            if (response.PreviousLevel < response.CurrentLevel)
                await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(ColorUtility.GetColor(CybermancyColor.Purple))
                    .WithTitle($"{member.Username}#{member.Discriminator}")
                    .WithDescription($"{member.Mention} has leveled to level {response.CurrentLevel}.")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build());

            
            if (rolesToAdd.Any())
                await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithColor(ColorUtility.GetColor(CybermancyColor.Gold))
                    .WithTitle($"{member.Username}#{member.Discriminator}")
                    .WithDescription($"{member.Mention} has earned {rolesToAdd.Select(x => x.Mention)}")
                    .WithFooter($"{member.Id}")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build());
        }
        #region UnusedEvents
        public Task DiscordOnMessageAcknowledged(DiscordClient sender, MessageAcknowledgeEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessageUpdated(DiscordClient sender, MessageUpdateEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessageDeleted(DiscordClient sender, MessageDeleteEventArgs args) => Task.CompletedTask;

        public Task DiscordOnMessagesBulkDeleted(DiscordClient sender, MessageBulkDeleteEventArgs args) => Task.CompletedTask;
        #endregion
    }
}
