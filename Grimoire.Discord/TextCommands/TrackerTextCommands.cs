// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.MessageLogging.Commands;

namespace Grimoire.Discord.TextCommands;

[RequireGuild]
[RequireModuleEnabled(Module.Leveling)]
[RequireUserGuildPermissions(Permissions.ManageMessages)]
[ModuleLifespan(ModuleLifespan.Transient)]
public class TrackerTextCommands(IMediator mediator) : BaseCommandModule
{
    private readonly IMediator _mediator = mediator;
    const ulong GuildId = 539925898128785460;

    [Command("Track")]
    public async Task TrackAsync(CommandContext ctx,
        DiscordUser user,
        TimeSpan duration,
        DiscordChannel? channel = null)
    {
        if (ctx.Guild.Id != GuildId)
            return;
        if (ctx.Member is null)
            return;
        if (user.Id == ctx.Client.CurrentUser.Id)
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithDescription("Why would I track myself?"));
            return;
        }

        if (!ctx.Guild.Members.TryGetValue(user.Id, out var member))
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithDescription("<_<\n>_>\nThat channel is not on this server.\n Try a different one."));
            return;
        }
        if (member.Permissions.HasPermission(Permissions.ManageGuild))
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithDescription("<_<\n>_>\nI can't track a mod.\n Try someone else"));
            return;
        }
        if (duration <= TimeSpan.Zero)
        {
            await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithDescription("Please select a positive duration value."));
            return;
        }
        channel ??= ctx.Channel;

        var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
        if (!permissions.HasPermission(Permissions.SendMessages))
            throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");

        var response = await this._mediator.Send(
            new AddTrackerCommand
            {
                UserId = member.Id,
                GuildId = ctx.Guild.Id,
                Duration = duration,
                ChannelId = channel.Id,
                ModeratorId = ctx.Member.Id,
            });
        await ctx.RespondAsync(new DiscordEmbedBuilder()
                .WithDescription($"Tracker placed on {member.Mention} in {channel.Mention} for {duration:d\\.hh\\:mm}"));

        if (response.ModerationLogId is null) return;
        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);
        if (logChannel is null) return;
        await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
        .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} placed a tracker on {member.Mention} in {channel.Mention} for {duration:d\\.hh\\:mm}")
            .WithColor(GrimoireColor.Purple));
    }

    [Command("Untrack")]
    public async Task UntrackAsync(CommandContext ctx, DiscordUser member)
    {
        if (ctx.Guild.Id != GuildId)
            return;
        if (ctx.Member is null)
            return;

        var response = await this._mediator.Send(new RemoveTrackerCommand{ UserId = member.Id, GuildId = ctx.Guild.Id});
        await ctx.RespondAsync(new DiscordEmbedBuilder()
        .WithDescription($"Tracker removed from {member.Mention}"));

        if (response.ModerationLogId is null) return;
        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLogId.Value);

        if (logChannel is null) return;
        await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} removed a tracker on {member.Mention}")
            .WithColor(GrimoireColor.Purple));
    }
}
