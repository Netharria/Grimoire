// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands.SetAutoPardon;
using Grimoire.Core.Features.Moderation.Commands.SetBanLogChannel;
using Grimoire.Core.Features.Moderation.Queries.GetModerationSettings;
using Grimoire.Discord.Enums;

namespace Grimoire.Discord.ModerationModule;

[SlashCommandGroup("ModSettings", "Changes the settings of the Moderation Module")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
public class ModerationSettingsCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("PublicBanLog", "Set public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")] DiscordChannel? channel = null)
    {
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(Permissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetBanLogChannelCommand
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel?.Id
        });

        if (option is ChannelOption.Off)
        {
            await ctx.ReplyAsync(message: $"Disabled the public ban log.", ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the public ban log.");
        }
        await ctx.ReplyAsync(message: $"Updated the public ban log to {channel?.Mention}", ephemeral: false);
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
        var response = await this._mediator.Send(new SetAutoPardonCommand
        {
            GuildId = ctx.Guild.Id,
            DurationAmount = durationType.GetTimeSpan(durationAmount)
        });

        await ctx.ReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType.GetName()}", ephemeral: false);
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"Auto pardon was updated by {ctx.User.Mention} " +
            $"to pardon sins after {durationAmount} {durationType.GetName()}.");
    }

    [SlashCommand("View", "See current moderation settings")]
    public async Task ViewSettingsAsync(InteractionContext ctx)
    {
        var response = await this._mediator.Send(new GetModerationSettingsQuery
        {
            GuildId = ctx.Guild.Id
        });

        var banLog = response.PublicBanLog is null ?
            "None" :
            ctx.Guild.GetChannel(response.PublicBanLog.Value).Mention;

        var autoPardonString =
            response.AutoPardonAfter.Days % 365 == 0
            ? $"{response.AutoPardonAfter.Days / 365} years"
            : response.AutoPardonAfter.Days % 30 == 0
            ? $"{response.AutoPardonAfter.Days / 30} months"
            : $"{response.AutoPardonAfter.Days} days";

        await ctx.ReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
                $"**Auto Pardon Duration:** {autoPardonString}\n" +
                $"**Ban Log:** {banLog}\n");
    }

}
