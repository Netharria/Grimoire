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
internal sealed class UpdateSinReason(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Reason")]
    [Description("Update the reason for a user's sin.")]
    public async Task ReasonAsync(CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be updated.")]
        SinId sinId,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason the sin will be updated to.")]
        string reason)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var userName = await dbContext.Sins
            .Where(sin => sin.Id == sinId && sin.GuildId == guild.GetGuildId())
            // ReSharper disable once AccessToDisposedClosure
            .Select(sin => (Username?)dbContext.UsernameHistory
                .Where(h => h.UserId == sin.UserId)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => h.Username)
                .FirstOrDefault())
            .FirstOrDefaultAsync();

        if (userName is null)
        {
            await ctx.SendErrorResponseAsync("Could not find a sin with that ID.");
            return;
        }

        dbContext.SinReasonHistory.Add(new SinReasonHistory
        {
            SinId = sinId, Reason = reason, ModeratorId = ctx.GetModeratorId(), SetAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor("Reason Updated")
            .AddField("Id", sinId.ToString(), true)
            .AddField("User", userName.Value.Value, true)
            .AddField("Reason", reason)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green));

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Green,
            Description = $"{ctx.User.Mention} updated reason to {reason} for **ID:** {sinId} **User:** {userName}"
        });
    }
}
