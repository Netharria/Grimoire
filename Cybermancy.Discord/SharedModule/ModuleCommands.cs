// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Enums;
using Cybermancy.Core.Features.Shared.Commands.ModuleCommands.EnableModuleCommand;
using Cybermancy.Core.Features.Shared.Queries.GetAllModuleStatesForGuild;
using Cybermancy.Discord.Enums;
using Cybermancy.Discord.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using MediatR;

namespace Cybermancy.Discord.SharedModule
{
    [SlashCommandGroup("Modules", "Enables or Disables the modules")]
    [SlashRequireGuild]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public class ModuleCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public ModuleCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("View", "View the current module states")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            var response = await this._mediator.Send(new GetAllModuleStatesForGuildQuery{ GuildId = ctx.Guild.Id});
            await ctx.ReplyAsync(
                title: "Current states of modules.",
                message: $"**Leveling Enabled:** {response.LevelingIsEnabled}\n" +
                $"**Logging Enabled:** {response.LoggingIsEnabled}\n" +
                $"**Moderation Enabled:** {response.ModerationIsEnabled}\n");
        }

        [SlashCommand("Set", "Enable or Disable a module")]
        public async Task SetAsync(InteractionContext ctx,
            [Option("Module", "The module to enable or disable")] Module module,
            [Option("Enable", "Whether to enable or disable the module")] bool enable)
        {
            var response = await this._mediator.Send(new EnableModuleCommand
            {
                GuildId = ctx.Guild.Id,
                Module = module,
                Enable = enable
            });

            if (!response.Success)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: response.Message);
                return;
            }

            await ctx.ReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module.GetName()}",
                ephemeral: false);

            if (response.ModerationLog is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.ModerationLog.Value);

            if (logChannel is null) return;
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} {(enable ? "Enabled" : "Disabled")} {module.GetName()}")
                .WithCybermancyColor(CybermancyColor.Purple));
        }
    }
}
