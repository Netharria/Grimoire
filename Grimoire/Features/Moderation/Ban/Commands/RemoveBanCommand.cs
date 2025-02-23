// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Exceptions;

namespace Grimoire.Features.Moderation.Ban.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.BanMembers], [])]
public sealed class RemoveBanCommand
{
    [Command("Unban")]
    [Description("Unbans a user from the server.")]
    public static async Task UnbanAsync(
        SlashCommandContext ctx,
        [Parameter("User")]
        [Description("The user to unban.")]
        DiscordUser user)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        try
        {
            await ctx.Guild.UnbanMemberAsync(user.Id);
            await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
                .WithAuthor("Unbanned")
                .AddField("User", user.Mention, true)
                .AddField("Moderator", ctx.User.Mention, true)
                .WithColor(GrimoireColor.Green));
        }
        catch (Exception ex) when (ex is NotFoundException or ServerErrorException)
        {
            var errorMessage = ex is NotFoundException
                ? "user could not be found."
                : "error when communicating with discord. Try again before asking for help.";
            await ctx.EditReplyAsync(
                GrimoireColor.Yellow,
                title: "Error",
                message: $"{user.GetUsernameWithDiscriminator()} was not unbanned because {errorMessage}");
        }
    }
}
