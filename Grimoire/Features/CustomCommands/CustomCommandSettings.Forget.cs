// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [UsedImplicitly]
    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Forget")]
    [Description("Forget a command that you have saved.")]
    public async Task Forget(
        CommandContext ctx,
        [SlashAutoCompleteProvider<GetCustomCommandOptions>]
        [Parameter("name")]
        [Description("The name of the command to forget.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var alreadyForgotten = await this.DeleteCommandAsync(guild.GetGuildId(), name);
        await OnForgetSuccess(ctx, guild, name, alreadyForgotten);
    }

    /// <summary>Deletes every version and the usage history. True when there was nothing to delete.</summary>
    private async Task<bool> DeleteCommandAsync(GuildId guildId, CustomCommandName name)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.CustomCommandUsages
            .Where(x => x.Name == name && x.GuildId == guildId)
            .ExecuteDeleteAsync();
        var deletedCount = await dbContext.CustomCommands
            .Where(x => x.Name == name && x.GuildId == guildId)
            .ExecuteDeleteAsync();
        return deletedCount == 0;
    }

    private async Task OnForgetSuccess(CommandContext ctx, DiscordGuild guild, CustomCommandName name,
        bool alreadyForgotten)
    {
        if (alreadyForgotten)
        {
            // Nothing changed, so there's nothing for the mod log.
            await ctx.ReplyAsync(GrimoireColor.Green, $"Command `{name}` was already forgotten.");
            return;
        }

        await ctx.ReplyAsync(GrimoireColor.Green, $"Removed command {name}");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Mention} asked {guild.CurrentMember.Mention} to forget command `{name}`.",
            Color = GrimoireColor.Purple
        });
    }
}
