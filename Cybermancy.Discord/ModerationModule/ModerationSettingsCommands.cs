// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Moderation.Commands.SetAutoPardon;
using Cybermancy.Core.Features.Moderation.Commands.SetBanLogChannel;
using Cybermancy.Discord.Attributes;
using Cybermancy.Discord.Extensions;
using Cybermancy.Discord.Structs;
using Cybermancy.Domain;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Mediator;

namespace Cybermancy.Discord.ModerationModule
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

        [SlashCommand("PublicBanLog", "Set a moderation setting.")]
        public async Task BanLogAsync(
            InteractionContext ctx,
            [Option("Value", "The channel. 0 is off. Empty is current channel")] string? value = null)
        {
            (var success, var result) = await ctx.TryMatchStringToChannelOrDefaultAsync(value);
            if (!success) return;

            var response = await this._mediator.Send(new SetBanLogChannelCommand
            {
                GuildId = ctx.Guild.Id,
                ChannelId = result == 0 ? null : result
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Updated Public Ban Log to {value}", ephemeral: false);
        }

        [SlashCommand("AutoPardon", "Updates how long till sins are pardoned.")]
        public async Task AutoPardonAsync(
            InteractionContext ctx,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")] Duration durationType,
            [Option("DurationAmount", "Select the amount of time before sins are auto pardoned.")] long durationAmount)
        {
            var response = await this._mediator.Send(new SetAutoPardonCommand
            {
                GuildId = ctx.Guild.Id,
                DurationType = durationType,
                DurationAmount = durationAmount
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"Will now auto pardon sins after {durationAmount} {durationType.GetName()}", ephemeral: false);
        }
    }
}
