// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Logging.Commands.TrackerCommands.AddTracker;
using Grimoire.Core.Features.Logging.Commands.TrackerCommands.RemoveTracker;

namespace Grimoire.Discord.LoggingModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.MessageLog)]
    [SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
    public class TrackerCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public TrackerCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Track", "Creates a log of a user's activity into the specificed channel.")]
        public async Task TrackAsync(InteractionContext ctx,
            [Option("User", "User to log.")] DiscordUser user,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
            [Minimum(0)]
            [Option("DurationAmount", "Select the amount of time the logging will last.")] long durationAmount,
            [Option("Channel", "Select the channel to log to. Current channel if left blank.")] DiscordChannel? discordChannel = null)
        {
            if (user.Id == ctx.Client.CurrentUser.Id)
            {
                await ctx.ReplyAsync(message: "Why would I track myself?");
                return;
            }

            if (!ctx.Guild.Members.TryGetValue(user.Id, out var member))
            {
                await ctx.ReplyAsync(message: "<_<\n>_>\nThat channel is not on this server.\n Try a different one.");
                return;
            }
            if (member.Permissions.HasPermission(Permissions.ManageGuild))
            {
                await ctx.ReplyAsync(message: "<_<\n>_>\nI can't track a mod.\n Try someone else");
                return;
            }

            discordChannel ??= ctx.Channel;

            var permissions = discordChannel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(Permissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");

            var response = await this._mediator.Send(
                new AddTrackerCommand
                {
                    UserId = member.Id,
                    GuildId = ctx.Guild.Id,
                    DurationType = durationType,
                    DurationAmount = durationAmount,
                    ChannelId = discordChannel.Id,
                    ModeratorId = ctx.Member.Id,
                });

            await ctx.ReplyAsync(message: $"Tracker placed on {member.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}", ephemeral: false);

            if (response.ModerationLogId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} placed a tracker on {member.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}")
                .WithColor(GrimoireColor.Purple));
        }

        [SlashCommand("Untrack", "Stops the logging of the user's activity")]
        public async Task UnTrackAsync(InteractionContext ctx,
            [Option("User", "User to stop logging.")] DiscordUser member)
        {
            var response = await this._mediator.Send(new RemoveTrackerCommand{ UserId = member.Id, GuildId = ctx.Guild.Id});


            await ctx.ReplyAsync(message: $"Tracker removed from {member.Mention}", ephemeral: false);

            if (response.ModerationLogId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator} removed a tracker on {member.Mention}")
                .WithColor(GrimoireColor.Purple));
        }
    }
}
