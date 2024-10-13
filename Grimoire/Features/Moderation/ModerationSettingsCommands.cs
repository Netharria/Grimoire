// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Moderation.Commands;
using Grimoire.Features.Moderation.Queries;

namespace Grimoire.Features.Moderation;

[SlashCommandGroup("ModSettings", "Changes the settings of the Moderation Module")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
internal sealed class ModerationSettingsCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("PublicBanLog", "Set public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync(true);
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetBanLogChannelCommand
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel?.Id
        });

        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: $"Disabled the public ban log.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the public ban log.");
            return;
        }
        await ctx.EditReplyAsync(message: $"Updated the public ban log to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated the public ban log to {channel?.Mention}.");
    }

    [SlashCommand("AutoPardon", "Updates how long till sins are pardoned.")]
    public async Task AutoPardonAsync(
        InteractionContext ctx,
        [Option("DurationType", "Select whether the duration will be in minutes hours or days")] Duration durationType,
        [Maximum(int.MaxValue)]
        [Minimum(0)]
        [Option("DurationAmount", "Select the amount of time before sins are auto pardoned.")] long durationAmount)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new SetAutoPardonCommand
        {
            GuildId = ctx.Guild.Id,
            DurationAmount = durationType.GetTimeSpan(durationAmount)
        });

        await ctx.EditReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType.GetName()}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"Auto pardon was updated by {ctx.User.Mention} " +
            $"to pardon sins after {durationAmount} {durationType.GetName()}.");
    }

    [SlashCommand("View", "See current moderation settings")]
    public async Task ViewSettingsAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);
        var response = await this._mediator.Send(new GetModerationSettingsQuery
        {
            GuildId = ctx.Guild.Id
        });

        var banLog = response.PublicBanLog is null ?
            "None" :
            ctx.Guild.Channels.GetValueOrDefault(response.PublicBanLog.Value)?.Mention;

        var autoPardonString =
            response.AutoPardonAfter.Days % 365 == 0
            ? $"{response.AutoPardonAfter.Days / 365} years"
            : response.AutoPardonAfter.Days % 30 == 0
            ? $"{response.AutoPardonAfter.Days / 30} months"
            : $"{response.AutoPardonAfter.Days} days";

        await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
                $"**Auto Pardon Duration:** {autoPardonString}\n" +
                $"**Ban Log:** {banLog}\n");
    }

}
