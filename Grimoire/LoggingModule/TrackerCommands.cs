// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.MessageLogging.Commands;

namespace Grimoire.LoggingModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.MessageLog)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
internal sealed class TrackerCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Track", "Creates a log of a user's activity into the specificed channel.")]
    public async Task TrackAsync(InteractionContext ctx,
        [Option("User", "User to log.")] DiscordUser user,
        [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
        [Minimum(0)]
        [Option("DurationAmount", "Select the amount of time the logging will last.")] long durationAmount,
        [Option("Channel", "Select the channel to log to. Current channel if left blank.")] DiscordChannel? discordChannel = null)
    {
        await ctx.DeferAsync();
        if (user.Id == ctx.Client.CurrentUser.Id)
        {
            await ctx.EditReplyAsync(message: "Why would I track myself?");
            return;
        }

        if (ctx.Guild.Members.TryGetValue(user.Id, out var member))
            if (member.Permissions.HasPermission(DiscordPermissions.ManageGuild))
            {
                await ctx.EditReplyAsync(message: "<_<\n>_>\nI can't track a mod.\n Try someone else");
                return;
            }


        discordChannel ??= ctx.Channel;

        if (!ctx.Guild.Channels.ContainsKey(discordChannel.Id))
        {
            await ctx.EditReplyAsync(message: "<_<\n>_>\nThat channel is not on this server.\n Try a different one.");
            return;
        }

        var permissions = discordChannel.PermissionsFor(ctx.Guild.CurrentMember);
        if (!permissions.HasPermission(DiscordPermissions.SendMessages))
            throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");

        var response = await this._mediator.Send(
            new AddTrackerCommand
            {
                UserId = user.Id,
                GuildId = ctx.Guild.Id,
                Duration = durationType.GetTimeSpan(durationAmount),
                ChannelId = discordChannel.Id,
                ModeratorId = ctx.Member.Id,
            });

        await ctx.EditReplyAsync(message: $"Tracker placed on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}");

        if (response.ModerationLogId is null) return;
        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

        if (logChannel is null) return;
        await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} placed a tracker on {user.Mention} in {discordChannel.Mention} for {durationAmount} {durationType.GetName()}")
            .WithColor(GrimoireColor.Purple));
    }

    [SlashCommand("Untrack", "Stops the logging of the user's activity")]
    public async Task UnTrackAsync(InteractionContext ctx,
        [Option("User", "User to stop logging.")] DiscordUser member)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new RemoveTrackerCommand{ UserId = member.Id, GuildId = ctx.Guild.Id});


        await ctx.EditReplyAsync(message: $"Tracker removed from {member.Mention}");

        if (response.ModerationLogId is null) return;
        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

        if (logChannel is null) return;
        await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} removed a tracker on {member.Mention}")
            .WithColor(GrimoireColor.Purple));
    }
}
