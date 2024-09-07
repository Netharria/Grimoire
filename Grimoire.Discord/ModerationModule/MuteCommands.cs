// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;

namespace Grimoire.Discord.ModerationModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageMessages)]
[SlashRequireBotPermissions(Permissions.ManageRoles)]
internal sealed class MuteCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Mute", "Prevents the user from being able to speak.")]
    public async Task MuteUserAsync(
        InteractionContext ctx,
        [Option("User", "The User to mute.")] DiscordUser user,
        [Option("DurationType", "Select whether the duration will be in minutes hours or days")] DurationType durationType,
        [Minimum(0)]
        [Option("DurationAmount", "Select the amount of time the mute will last.")] long durationAmount,
        [MaximumLength(1000)]
        [Option("Reason", "The reason why the user is getting muted.")] string? reason = null
        )
    {
        await ctx.DeferAsync();
        if (user is not DiscordMember member) throw new AnticipatedException("That user is not on the server.");
        if (ctx.Guild.Id == member.Id) throw new AnticipatedException("That user is not on the server.");
        var response = await this._mediator.Send(new MuteUserCommand
        {
            UserId = member.Id,
            GuildId = ctx.Guild.Id,
            DurationAmount = durationAmount,
            DurationType = durationType,
            ModeratorId = ctx.User.Id,
            Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason
        });
        var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
        if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
        await member.GrantRoleAsync(muteRole, reason!);

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Mute")
            .AddField("User", user.Mention, true)
            .AddField("Sin Id", $"**{response.SinId}**", true)
            .AddField("Moderator", ctx.User.Mention, true)
            .AddField("Length", $"{durationAmount} {durationType.GetName()}", true)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(reason))
            embed.AddField("Reason", reason);

        await ctx.EditReplyAsync(embed: embed);

        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"Mute Id {response.SinId}")
            .WithDescription($"You have been muted for {durationAmount} {durationType.GetName()} by {ctx.User.Mention} for {reason}")
            .WithColor(GrimoireColor.Red));
        }
        catch (Exception)
        {
            await ctx.SendLogAsync(response, GrimoireColor.Red,
                message: $"Was not able to send a direct message with the mute details to {user.Mention}");
        }

        if (response.LogChannelId is null) return;

        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

        if (logChannel is null) return;

        await logChannel.SendMessageAsync(embed);
    }

    [SlashCommand("Unmute", "Removes the mute on the user allowing them to speak.")]
    public async Task UnmuteUserAsync(
        InteractionContext ctx,
        [Option("User", "The User to unmute.")] DiscordUser user)
    {
        await ctx.DeferAsync();
        if (user is not DiscordMember member) throw new AnticipatedException("That user is not on the server.");
        if (ctx.Guild.Id == member.Id) throw new AnticipatedException("That user is not on the server.");
        var response = await this._mediator.Send(new UnmuteUserCommand
        {
            UserId = member.Id,
            GuildId = ctx.Guild.Id
        });
        var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
        if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
        await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.Member.Mention}");

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Unmute")
            .AddField("User", user.Mention, true)
            .AddField("Moderator", ctx.User.Mention, true)
            .WithColor(GrimoireColor.Green)
            .WithTimestamp(DateTimeOffset.UtcNow);


        await ctx.EditReplyAsync(embed: embed);

        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"Unmuted")
            .WithDescription($"You have been unmuted by {ctx.User.Mention}")
            .WithColor(GrimoireColor.Green));
        }
        catch (Exception)
        {
            await ctx.SendLogAsync(response, GrimoireColor.Red,
                message: $"Was not able to send a direct message with the unmute details to {user.Mention}");
        }

        if (response.LogChannelId is null) return;

        var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

        if (logChannel is null) return;

        await logChannel.SendMessageAsync(embed);

    }
}
