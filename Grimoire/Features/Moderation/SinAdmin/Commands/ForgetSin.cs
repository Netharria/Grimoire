// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Reflection.Metadata.Ecma335;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
internal sealed class ForgetSin(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Forget")]
    [Description("Forget a user's sin. This will permanently remove the sin from the bots memory.")]
    public async Task ForgetAsync(CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be forgotten.")]
        int sinId)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .Where(sin => sin.Id == sinId)
            .Where(sin => sin.GuildId == guild.Id)
            .Select(sin => new
            {
                // ReSharper disable AccessToDisposedClosure
                Sin = sin,
                UserName = dbContext.UsernameHistory
                    .Where(usernameHistory => usernameHistory.UserId == sin.UserId)
                    .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                    .Select(usernameHistory => usernameHistory.Username)
                    .FirstOrDefault()
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not find a sin with that ID.");
            return;
        }


        dbContext.Sins.Remove(result.Sin);
        await dbContext.SaveChangesAsync();

        var message = $"**ID:** {result.Sin.Id} **User:** {result.UserName}";

        await ctx.EditReplyAsync(GrimoireColor.Green, message, "Forgot");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor($"{guild.CurrentMember.Nickname} has been commanded to forget.")
                .AddField("User", result.UserName ?? "Unknown", true)
                .AddField("Sin Id", result.Sin.Id.ToString(), true)
                .AddField("Moderator", ctx.User.Mention, true)
                .WithColor(GrimoireColor.Green)
        });
    }
}
