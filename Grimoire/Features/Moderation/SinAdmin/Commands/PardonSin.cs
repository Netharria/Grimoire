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
internal sealed class PardonSin(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    [Command("Pardon")]
    [Description("Pardon a user's sin. This leaves the sin in the logs but marks it as pardoned.")]
    public async Task PardonAsync(CommandContext ctx,
        [MinMaxValue(1)] [Parameter("sin_id")] [Description("The id of the sin to be pardoned.")]
        SinId sinId,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason the sin is getting pardoned.")]
        string reason)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        if (ModerationReason.Create(reason) is not Validation<ModerationReason>.Valid(var pardonReason))
        {
            await ctx.SendErrorResponseAsync("The reason can't be empty.");
            return;
        }

        var sinner = await dbContext.Sins
            .Where(sin => sin.Id == sinId && sin.GuildId == guild.GetGuildId())
            .Select(sin => (UserId?)sin.UserId)
            .FirstOrDefaultAsync();

        if (sinner is not { } userId)
        {
            await ctx.ReplyAsync(GrimoireColor.Red, "Could not find a sin with that ID.");
            return;
        }

        var userName = await dbContext.UsernameHistory.GetDisplayNameAsync(userId);

        dbContext.Pardons.Add(new Pardon
        {
            SinId = sinId,
            GuildId = guild.GetGuildId(),
            ModeratorId = ctx.GetModeratorId(),
            Reason = pardonReason,
            SetAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var message = $"**ID:** {sinId} **User:** {userName}";

        await ctx.ReplyAsync(GrimoireColor.Green, message, "Pardoned");

        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Embed = new DiscordEmbedBuilder()
                .WithAuthor("Pardon")
                .AddField("User", userName, true)
                .AddField("Sin Id", sinId.ToString(), true)
                .AddField("Moderator", ctx.User.Mention, true)
                .AddField("Reason", reason, true)
                .WithColor(GrimoireColor.Green)
        });
    }
}
