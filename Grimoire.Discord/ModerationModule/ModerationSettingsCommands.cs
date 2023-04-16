// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands.SetAutoPardon;
using Grimoire.Core.Features.Moderation.Commands.SetBanLogChannel;
using Grimoire.Domain;

namespace Grimoire.Discord.ModerationModule
{
    [SlashCommandGroup("ModerationSettings", "Changes the settings of the Moderation Module")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class ModerationSettingsCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public ModerationSettingsCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("PublicBanLog", "Set public channel to publish ban and unbans to.")]
        public async Task BanLogAsync(
            InteractionContext ctx,
            [Choice("Off", 0)]
            [Choice("CurrentChannel", 1)]
            [Choice("SelectChannel", 2)]
            [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] long option,
            [Option("Channel", "The channel to send. ")] DiscordChannel? value)
        {
            ulong? channelId;
            switch (option)
            {
                case 0:
                    channelId = null;
                    break;
                case 1:
                    channelId = ctx.Channel.Id;
                    break;
                case 2:
                    if (value is not null)
                    {
                        channelId = value.Id;
                        break;
                    }
                    await ctx.ReplyAsync(GrimoireColor.Orange, message: "Please specify a channel.");
                    return;
                default:
                    await ctx.ReplyAsync(GrimoireColor.Orange, message: "Options selected are not valid.");
                    return;
            }
            await this._mediator.Send(new SetBanLogChannelCommand
            {
                GuildId = ctx.Guild.Id,
                ChannelId = channelId
            });


            await ctx.ReplyAsync(message: $"Updated Public Ban Log to {value}", ephemeral: false);
        }

        [SlashCommand("AutoPardon", "Updates how long till sins are pardoned.")]
        public async Task AutoPardonAsync(
            InteractionContext ctx,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] Duration durationType,
            [Maximum(int.MaxValue)]
            [Minimum(0)]
            [Option("DurationAmount", "Select the amount of time before sins are auto pardoned.")] long durationAmount)
        {
            await this._mediator.Send(new SetAutoPardonCommand
            {
                GuildId = ctx.Guild.Id,
                DurationType = durationType,
                DurationAmount = durationAmount
            });

            await ctx.ReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType.GetName()}", ephemeral: false);
        }
    }
}
