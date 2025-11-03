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
        [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
        [Parameter("Name")]
        [Description("The name of the command to forget.")]
        CustomCommandName name)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        await dbContext.CustomCommands
            .Where(x => x.Name == name && x.GuildId == guild.GetGuildId())
            .ExecuteDeleteAsync();

        await ctx.EditReplyAsync(GrimoireColor.Green, $"Forgot command: {name}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Mention} asked {guild.CurrentMember} to forget command: {name}",
            Color = GrimoireColor.Purple
        });
    }
}
