// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Exceptions;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.Ban.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
[RequirePermissions([DiscordPermission.BanMembers], [])]
public sealed class RemoveBanCommand(BanHandoff banHandoff)
{
    private readonly BanHandoff _banHandoff = banHandoff;

    [Command("Unban")]
    [Description("Unbans a user from the server.")]
    public async Task UnbanAsync(
        CommandContext ctx,
        [Parameter("user")] [Description("The user to unban.")]
        DiscordUser user)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        this._banHandoff.ExpectUnban(guild.GetGuildId(), user.GetUserId(), ctx.GetModeratorId());
        try
        {
            await guild.UnbanMemberAsync(user.Id);
            await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
                .WithAuthor("Unbanned")
                .AddField("User", user.Mention, true)
                .AddField("Moderator", ctx.User.Mention, true)
                .WithColor(GrimoireColor.Green));
        }
        catch (Exception ex) when (ex is NotFoundException or ServerErrorException)
        {
            this._banHandoff.CancelUnban(guild.GetGuildId(), user.GetUserId());
            var errorMessage = ex is NotFoundException
                ? "user is not banned."
                : "error when communicating with discord. Try again before asking for help.";
            await ctx.ReplyAsync(
                GrimoireColor.Yellow,
                title: "Error",
                message: $"{user.Username} was not unbanned because {errorMessage}");
        }
    }
}
