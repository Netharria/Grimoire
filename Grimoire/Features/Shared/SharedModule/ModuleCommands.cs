// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;
using Grimoire.Features.Shared.Queries;

namespace Grimoire.Features.Shared.SharedModule;

[SlashCommandGroup("Modules", "Enables or Disables the modules")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
internal sealed class ModuleCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current module states")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);
        var response = await this._mediator.Send(new GetAllModuleStatesForGuildQuery{ GuildId = ctx.Guild.Id});
        if (response is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Red, "Settings could not be found for this server.");
            return;
        }
        await ctx.EditReplyAsync(
            title: "Current states of modules.",
            message: $"**Leveling Enabled:** {response.LevelingIsEnabled}\n" +
            $"**User Log Enabled:** {response.UserLogIsEnabled}\n" +
            $"**Message Log Enabled:** {response.MessageLogIsEnabled}\n" +
            $"**Moderation Enabled:** {response.ModerationIsEnabled}\n" +
            $"**Commands Enabled:** {response.CommandsIsEnabled}\n");
    }

    [SlashCommand("Set", "Enable or Disable a module")]
    public async Task SetAsync(InteractionContext ctx,
        [Option("Module", "The module to enable or disable")] Module module,
        [Option("Enable", "Whether to enable or disable the module")] bool enable)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new EnableModuleCommand
        {
            GuildId = ctx.Guild.Id,
            Module = module,
            Enable = enable
        });
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.GetUsernameWithDiscriminator()} {(enable ? "Enabled" : "Disabled")} {module.GetName()}");
        await ctx.EditReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module.GetName()}");
    }
}
