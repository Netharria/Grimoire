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
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is not { } guild)
        {
            await ctx.SendWarningResponseAsync("You need to be in a guild to use this command.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var guildId = guild.GetGuildId();
        try
        {
            var deletedCount = await dbContext.CustomCommands
                .Where(x => x.Name == name && x.GuildId == guildId)
                .ExecuteDeleteAsync();

            var alreadyForgotten = deletedCount == 0;

            await ctx.ReplyAsync(GrimoireColor.Green,
                alreadyForgotten
                    ? $"Command `{name}` was already forgotten."
                    : $"Removed command {name}");
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guildId,
                GuildLogType = GuildLogType.Moderation,
                Description = alreadyForgotten
                    ? $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command `{name}` (already absent)."
                    : $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command `{name}`.",
                Color = GrimoireColor.Purple
            });
        }
        catch (DbUpdateException)
        {
            await ctx.SendErrorResponseAsync(
                "Could not forget that command right now due to a database error. Please try again.");
        }
    }
}
