// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
internal sealed class ForgetSin(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    [Command("Forget")]
    [Description("Forget a user's sin. This will permanently remove the sin from the bots memory.")]
    public async Task ForgetAsync(CommandContext ctx,
        [MinMaxValue(1)] [Parameter("sin_id")] [Description("The id of the sin to be forgotten.")]
        SinId sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var sin = await dbContext.Sins.FirstOrDefaultAsync(sin => sin.Id == sinId && sin.GuildId == guild.GetGuildId());
        if (sin is null)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not find a sin with that ID.");
            return;
        }

        var userName = await dbContext.UsernameHistory.GetDisplayNameAsync(sin.UserId);
        dbContext.Sins.Remove(sin);
        await dbContext.SaveChangesAsync();

        var message = $"**ID:** {sinId} **User:** {userName}";

        await ctx.ReplyAsync(GrimoireColor.Green, message, "Forgot");

        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor($"{guild.CurrentMember.DisplayName} has been commanded to forget.")
                .AddField("User", userName, true)
                .AddField("Sin Id", sinId.ToString(), true)
                .AddField("Moderator", ctx.User.Mention, true)
                .WithColor(GrimoireColor.Green)
        });
    }
}
