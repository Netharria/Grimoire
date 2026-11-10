// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Awards;

public sealed class ReclaimUserXp(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    public enum XpOption
    {
        [ChoiceDisplayName("Take all their xp.")]
        All,

        [ChoiceDisplayName("Take a specific amount.")]
        Amount
    }


    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Reclaim")]
    [Description("Takes away xp from user.")]
    public async Task ReclaimAsync(CommandContext ctx,
        [Parameter("user")] [Description("The user to take xp from.")]
        DiscordUser user,
        [Parameter("option")] [Description("Select either to take all of their xp or a specific amount.")]
        XpOption option,
        [MinMaxValue(0)] [Parameter("amount")] [Description("The amount of xp to take.")]
        int amount = 0)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        await ValidateOption(option, amount)
            .BindAsync(_ => this.ReclaimAsync(user, guild, option, amount, ctx.GetModeratorId()))
            .MatchAsync(
                xpTaken => this.ReportReclaimAsync(ctx, user, guild, xpTaken),
                error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    internal static Result<Unit> ValidateOption(XpOption option, int amount)
        => option == XpOption.Amount && amount == 0
            ? Result<Unit>.Fail(new Error("reclaim.amount.zero", "Specify an amount greater than 0"))
            : Result<Unit>.Ok(Unit.Value);

    /// <summary>Takes the XP and returns how much was taken: everything, or the amount, capped at what they have.</summary>
    private async Task<Result<long>> ReclaimAsync(DiscordUser user, DiscordGuild guild, XpOption option, int amount,
        ModeratorId moderatorId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var memberXp = await dbContext.XpHistory
            .Where(history => history.UserId == user.GetUserId() && history.GuildId == guild.GetGuildId())
            .SumAsync(history => history.RawXp);
        var xpToTake = option == XpOption.All ? memberXp : Math.Min(memberXp, amount);
        if (xpToTake <= 0)
            return new Result<long>.NotFound(new Error("reclaim.member.no-xp", $"{user.Mention} has no xp to take."));

        var reclaimedXp = NegativeXpAmount.Create(-xpToTake)
            .Map(xp => ReclaimedXp.Create(xp, moderatorId, user.GetUserId(), guild.GetGuildId(), DateTimeOffset.UtcNow))
            .Match(r => r, _ => throw new InvariantViolationException());
        await dbContext.XpHistory.AddAsync(reclaimedXp);
        await dbContext.SaveChangesAsync();
        return Result<long>.Ok(xpToTake);
    }

    private async Task ReportReclaimAsync(CommandContext ctx, DiscordUser user, DiscordGuild guild, long xpTaken)
    {
        await ctx.ReplyAsync(GrimoireColor.DarkPurple, $"{xpTaken} xp has been taken from {user.Mention}.");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = $"{xpTaken} xp has been taken from {user.Mention} by {ctx.User.Mention}.",
            Color = GrimoireColor.Purple
        });
    }
}
