// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
internal sealed class PardonSin(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Pardon")]
    [Description("Pardon a user's sin. This leaves the sin in the logs but marks it as pardoned.")]
    public async Task PardonAsync(CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be pardoned.")]
        SinId sinId,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason the sin is getting pardoned.")]
        string reason = "")
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .Where(sin => sin.Id == sinId && sin.GuildId == guild.GetGuildId())
            // ReSharper disable once AccessToDisposedClosure
            .Select(sin => (Username?)dbContext.UsernameHistory
                .Where(h => h.UserId == sin.UserId)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => h.Username)
                .FirstOrDefault())
            .FirstOrDefaultAsync();

        if (result is not { Value: var userName })
        {
            await ctx.ReplyAsync(GrimoireColor.Red, "Could not find a sin with that ID.");
            return;
        }

        dbContext.Pardons.Add(new Pardon
        {
            SinId = sinId,
            GuildId = guild.GetGuildId(),
            ModeratorId = ctx.GetModeratorId(),
            Reason = ModerationReason.FromDatabase(reason),
            SetAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var message = $"**ID:** {sinId} **User:** {userName}";

        await ctx.ReplyAsync(GrimoireColor.Green, message, "Pardoned");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Pardon")
                .AddField("User", userName, true)
                .AddField("Sin Id", sinId.ToString(), true)
                .AddField("Moderator", ctx.User.Mention, true)
                .AddField("Reason", string.IsNullOrWhiteSpace(reason) ? "None" : reason, true)
                .WithColor(GrimoireColor.Green)
        });
    }
}
