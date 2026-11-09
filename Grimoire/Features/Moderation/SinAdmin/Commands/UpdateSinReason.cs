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
internal sealed class UpdateSinReason(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Reason")]
    [Description("Update the reason for a user's sin.")]
    public async Task ReasonAsync(CommandContext ctx,
        [MinMaxValue(1)] [Parameter("sin_id")] [Description("The id of the sin to be updated.")]
        SinId sinId,
        [MinMaxLength(maxLength: 1000)] [Parameter("reason")] [Description("The reason the sin will be updated to.")]
        string reason)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        // M-7: a blank reason used to reach an InvariantViolationException.
        if (ModerationReason.Create(reason) is not Validation<ModerationReason>.Valid(var validReason))
        {
            await ctx.SendErrorResponseAsync("The reason can't be empty.");
            return;
        }

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var sinner = await dbContext.Sins
            .Where(sin => sin.Id == sinId && sin.GuildId == guild.GetGuildId())
            .Select(sin => (UserId?)sin.UserId)
            .FirstOrDefaultAsync();

        if (sinner is not { } userId)
        {
            await ctx.SendErrorResponseAsync("Could not find a sin with that ID.");
            return;
        }

        var userName = await dbContext.UsernameHistory.GetDisplayNameAsync(userId);

        dbContext.SinReasonHistory.Add(new SinReasonHistory
        {
            SinId = sinId,
            Reason = validReason,
            Actor = new ModerationActor.Moderator(ctx.GetModeratorId()),
            SetAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor("Reason Updated")
            .AddField("Id", sinId.ToString(), true)
            .AddField("User", userName, true)
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
