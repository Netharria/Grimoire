// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Logging.Commands.TrackerCommands.AddTracker;
using Cybermancy.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker;
using Cybermancy.Discord.Attributes;
using Cybermancy.Discord.Enums;
using Cybermancy.Discord.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.Discord.LoggingModule
{


    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Logging)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class TrackerCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public TrackerCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Track", "Creates a log of a user's activity into the specificed channel.")]
        public async Task TrackAsync(InteractionContext ctx,
            [Option("User", "User to log.")] DiscordMember member,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
            [Option("DurationAmount", "Select the amount of time the logging will last.")] long durationAmount,
            [Option("Channel", "Select the channel to log to. Current channel if left blank.")] DiscordChannel? discordChannel = null)
        {
            if (member.Permissions.HasPermission(Permissions.ManageGuild)) await ctx.ReplyAsync(message: "<_<\n>_>\nI can't track a mod.\n Try someone else");
            if (discordChannel is null) discordChannel = ctx.Channel;
            if (!ctx.Guild.Channels.ContainsKey(discordChannel.Id)) await ctx.ReplyAsync(message: "<_<\n>_>\nThat channel is not on this server.\n Try a different one.");
            var response = await _mediator.Send(
                new AddTrackerCommand
                {
                    UserId = member.Id,
                    GuildId = member.Guild.Id,
                    DurationType = durationType,
                    DurationAmount = durationAmount,
                    ChannelId = discordChannel.Id,
                    ModeratorId = ctx.Member.Id,
                });
            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Tracker placed on {member.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}", ephemeral: false);

            if (response.ModerationLogId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} placed a tracker on {member.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}")
                .WithCybermancyColor(CybermancyColor.Purple));
        }

        [SlashCommand("Untrack", "Stops the logging of the user's activity")]
        public async Task UnTrackAsync(InteractionContext ctx,
            [Option("User", "User to stop logging.")] DiscordMember member)
        {
            var response = await this._mediator.Send(new RemoveTrackerCommand{ UserId = member.Id, GuildId = member.Guild.Id});

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Tracker removed from {member.Mention}", ephemeral: false);

            if (response.ModerationLogId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} removed a tracker on {member.Mention}")
                .WithCybermancyColor(CybermancyColor.Purple));
        }
    }
}
