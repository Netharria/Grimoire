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
        var guild = ctx.Guild!;
        await DeleteCommandAsync(guild.GetGuildId(), name)
            .Match(
            alreadyForgotten => OnForgetSuccess(ctx, guild, name, alreadyForgotten),
            error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    private async Task<Result<bool>> DeleteCommandAsync(GuildId guildId, CustomCommandName name)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.CustomCommandUsages
                .Where(x => x.Name == name && x.GuildId == guildId)
                .ExecuteDeleteAsync();
            var deletedCount = await dbContext.CustomCommands
                .Where(x => x.Name == name && x.GuildId == guildId)
                .ExecuteDeleteAsync();
            return Result<bool>.Ok(deletedCount == 0);
        }
        catch (Exception)
        {
            return Result<bool>.Fail(new Error("command.forget.db_error",
                "Could not forget that command right now due to a database error. Please try again."));
        }
    }

    private async Task OnForgetSuccess(CommandContext ctx, DiscordGuild guild, CustomCommandName name, bool alreadyForgotten)
    {
        await ctx.ReplyAsync(GrimoireColor.Green,
            alreadyForgotten
                ? $"Command `{name}` was already forgotten."
                : $"Removed command {name}");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = alreadyForgotten
                ? $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command `{name}` (already absent)."
                : $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command `{name}`.",
            Color = GrimoireColor.Purple
        });
    }
}
