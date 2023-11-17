// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Shared.Commands;
using Grimoire.Core.Features.Shared.Queries;

namespace Grimoire.Discord.SharedModule;

[SlashCommandGroup("Modules", "Enables or Disables the modules")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
public class ModuleCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current module states")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        var response = await this._mediator.Send(new GetAllModuleStatesForGuildQuery{ GuildId = ctx.Guild.Id});
        await ctx.ReplyAsync(
            title: "Current states of modules.",
            message: $"**Leveling Enabled:** {response.LevelingIsEnabled}\n" +
            $"**User Log Enabled:** {response.UserLogIsEnabled}\n" +
            $"**Message Log Enabled:** {response.MessageLogIsEnabled}\n" +
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
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.GetUsernameWithDiscriminator()} {(enable ? "Enabled" : "Disabled")} {module.GetName()}");
        await ctx.ReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module.GetName()}",
            ephemeral: false);
    }
}
