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

    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Reclaim")]
    [Description("Takes away xp from user.")]
    public async Task ReclaimAsync(CommandContext ctx,
        [Parameter("User")] [Description("The user to take xp from.")]
        DiscordUser user,
        [Parameter("Option")] [Description("Select either to take all of their xp or a specific amount.")]
        XpOption option,
        [MinMaxValue(0)] [Parameter("Amount")] [Description("The amount of xp to take.")]
        int amount = 0)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await ValidateOption(option, amount)
            .BindAsync(_ => this.FetchXpToTakeAsync(user, guild, option, amount))
            .BindAsync(xpToTake => this.PersistReclaimAsync(ctx, user, guild, xpToTake))
            .MatchAsync(
                _ => Task.CompletedTask,
                error => ctx.SendErrorResponseAsync(error.Message).AsTask());
    }

    internal static Result<Unit> ValidateOption(XpOption option, int amount)
        => option == XpOption.Amount && amount == 0
            ? Result<Unit>.Fail(new Error("reclaim.amount.zero", "Specify an amount greater than 0"))
            : Result<Unit>.Ok(Unit.Value);

    private async Task<Result<long>> FetchXpToTakeAsync(
        DiscordUser user,
        DiscordGuild guild,
        XpOption option,
        int amount)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

        var memberXp = await dbContext.XpHistory
            .AsNoTracking()
            .Where(history => history.UserId == user.GetUserId() && history.GuildId == guild.GetGuildId())
            .SumAsync(history => (long?)history.RawXp);

        if (memberXp is null or 0)
            return new Result<long>.NotFound(
                new Error("reclaim.member.no-xp", $"{user.Mention} has no xp to take."));

        var xpToTake = Math.Min(memberXp.Value, option switch
        {
            XpOption.All => memberXp.Value,
            XpOption.Amount => amount,
            _ => throw new ArgumentOutOfRangeException(nameof(option), "XpOption not implemented in switch statement.")
        });

        return xpToTake <= 0
            ? new Result<long>.NotFound(new Error("reclaim.member.no-xp", $"{user.Mention} has no xp to take."))
            : Result<long>.Ok(xpToTake);
    }

    private async Task<Result<Unit>> PersistReclaimAsync(
        CommandContext ctx,
        DiscordUser user,
        DiscordGuild guild,
        long xpToTake)
    {
        var reclaimedXp = NegativeXpAmount.Create(-xpToTake)
            .Bind(xp => ReclaimedXp.Create(xp, user.GetUserId(), guild.GetGuildId(), DateTimeOffset.UtcNow))
            .Match(r => r, _ => throw new UnreachableException());

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        await dbContext.XpHistory.AddAsync(reclaimedXp);
        await dbContext.SaveChangesAsync();

        await ctx.ReplyAsync(GrimoireColor.DarkPurple,
            $"{xpToTake} xp has been taken from {user.Mention}.");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Description = $"{xpToTake} xp has been taken from {user.Mention} by {ctx.User.Mention}.",
            Color = GrimoireColor.Purple
        });

        return Result<Unit>.Ok(Unit.Value);
    }
}
